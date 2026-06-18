using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Authorize(Roles = AppRoles.Admin)]
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
}
