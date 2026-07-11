using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Media uploads for admin forms (product images/documents, category thumbnails, ...).
/// The file is stored on local disk and a <see cref="Medium"/> row is created; forms then
/// reference the returned media id.
/// </summary>
[ApiController]
[RequirePermission(Permissions.MediaManage)]
[Route("api/admin/media")]
public sealed class AdminMediaController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".svg" };

    private static readonly HashSet<string> AllowedFileExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".txt" };

    private readonly StoreDbContext _db;
    private readonly IMediaStorage _storage;

    public AdminMediaController(StoreDbContext db, IMediaStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>
    /// Media library listing (paged, newest first). <paramref name="search"/> filters by FileName/Caption.
    /// Each row carries a <c>referenceCount</c> = ProductMedia rows + products using it as a thumbnail +
    /// categories using it as a thumbnail, so the UI can warn before deleting an in-use asset.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MediaListResponse>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Media.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m =>
                (m.FileName != null && m.FileName.Contains(search))
                || (m.Caption != null && m.Caption.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.FileName,
                m.Caption,
                m.MediaType,
                m.FileSize,
                ReferenceCount =
                    _db.ProductMedia.Count(pm => pm.MediaId == m.Id)
                    + _db.Products.Count(p => p.ThumbnailImageId == m.Id)
                    + _db.Categories.Count(c => c.ThumbnailImageId == m.Id)
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new MediaListItemDto(
                r.Id, r.FileName, _storage.GetUrl(r.FileName)!, r.Caption, r.MediaType, r.FileSize, r.ReferenceCount))
            .ToList();

        return Ok(new MediaListResponse(items, totalCount, page, pageSize));
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ActionResult<MediaDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { error = "The uploaded file is empty." });
        }

        var extension = Path.GetExtension(file.FileName);
        int mediaType;
        if (AllowedImageExtensions.Contains(extension))
        {
            mediaType = MediaTypes.Image;
        }
        else if (AllowedFileExtensions.Contains(extension))
        {
            mediaType = MediaTypes.File;
        }
        else
        {
            return BadRequest(new { error = $"File type '{extension}' is not allowed." });
        }

        await using var stream = file.OpenReadStream();
        var fileName = await _storage.SaveAsync(stream, file.FileName, cancellationToken);

        var medium = new Medium
        {
            FileName = fileName,
            FileSize = (int)file.Length,
            MediaType = mediaType,
            Caption = file.FileName
        };
        _db.Media.Add(medium);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new MediaDto(medium.Id, medium.FileName, _storage.GetUrl(medium.FileName)!, medium.Caption, medium.MediaType));
    }

    /// <summary>
    /// Deletes an unreferenced media asset (row + local file). Returns 404 if it does not exist, or 409 when
    /// it is still referenced by a product image, a product thumbnail or a category thumbnail.
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var medium = await _db.Media.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (medium == null)
        {
            return NotFound();
        }

        var referenceCount =
            await _db.ProductMedia.CountAsync(pm => pm.MediaId == id, cancellationToken)
            + await _db.Products.CountAsync(p => p.ThumbnailImageId == id, cancellationToken)
            + await _db.Categories.CountAsync(c => c.ThumbnailImageId == id, cancellationToken);

        if (referenceCount > 0)
        {
            return Conflict(new { error = "This media is still in use and cannot be deleted." });
        }

        _storage.Delete(medium.FileName);
        _db.Media.Remove(medium);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
