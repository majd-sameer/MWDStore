using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Common;
using Store.Application.Content;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>
/// Public content: CMS pages by slug (old Cms module), news listing/detail (old News module),
/// the contact form (old Contacts module) and admin-editable homepage content blocks.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api")]
public sealed class ContentController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;
    private readonly IRequestCulture _culture;
    private readonly IContentBlockService _contentBlocks;

    public ContentController(
        StoreDbContext db,
        TimeProvider timeProvider,
        IMediaUrlBuilder mediaUrl,
        IRequestCulture culture,
        IContentBlockService contentBlocks)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
        _culture = culture;
        _contentBlocks = contentBlocks;
    }

    // ----- Content blocks -------------------------------------------------------------------------

    /// <summary>Published homepage content blocks, optionally narrowed to a key prefix (e.g.
    /// <c>?prefix=home</c>), localized for the request culture. Missing/unpublished keys simply
    /// don't appear — callers (storefront sections) fall back to their built-in copy.</summary>
    [HttpGet("content/blocks")]
    public async Task<ActionResult<IReadOnlyList<ContentBlockDto>>> Blocks(
        [FromQuery] string? prefix, CancellationToken cancellationToken)
    {
        var blocks = await _contentBlocks.GetPublishedAsync(prefix, cancellationToken);
        return Ok(blocks);
    }

    // ----- CMS pages ----------------------------------------------------------------------------

    [HttpGet("pages/{slug}")]
    public async Task<ActionResult<PublicPageDto>> Page(string slug, CancellationToken cancellationToken)
    {
        var page = await _db.Pages
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.IsPublished && !p.IsDeleted)
            .Select(p => new { p.Id, p.Name, p.Slug, p.Body, p.MetaTitle, p.MetaKeywords, p.MetaDescription })
            .FirstOrDefaultAsync(cancellationToken);

        if (page == null)
        {
            return NotFound();
        }

        var lang = _culture.Language;
        return Ok(new PublicPageDto(
            page.Name.Resolve(lang)!,
            page.Slug,
            page.Body?.Resolve(lang),
            page.MetaTitle?.Resolve(lang),
            page.MetaKeywords?.Resolve(lang),
            page.MetaDescription?.Resolve(lang)));
    }

    // ----- News ----------------------------------------------------------------------------------

    [HttpGet("news")]
    public async Task<ActionResult<IReadOnlyList<NewsListItemDto>>> News(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var rows = await _db.NewsItems
            .AsNoTracking()
            .Where(n => n.IsPublished && !n.IsDeleted)
            .OrderByDescending(n => n.PublishedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.Id, n.Name, n.Slug, n.ShortContent,
                ThumbnailFileName = n.ThumbnailImage != null ? n.ThumbnailImage.FileName : null,
                n.PublishedOn
            })
            .ToListAsync(cancellationToken);

        var lang = _culture.Language;
        return Ok(rows.Select(r => new NewsListItemDto(
            r.Id, r.Name.Resolve(lang)!, r.Slug, r.ShortContent?.Resolve(lang),
            _mediaUrl.GetUrl(r.ThumbnailFileName), r.PublishedOn)).ToList());
    }

    [HttpGet("news/{slug}")]
    public async Task<ActionResult<NewsDetailDto>> NewsDetail(string slug, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems
            .Include(n => n.ThumbnailImage)
            .FirstOrDefaultAsync(n => n.Slug == slug && n.IsPublished && !n.IsDeleted, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        var lang = _culture.Language;
        return Ok(new NewsDetailDto(
            item.Id,
            item.Name.Resolve(lang)!,
            item.Slug,
            item.ShortContent?.Resolve(lang),
            item.FullContent?.Resolve(lang),
            _mediaUrl.GetUrl(item.ThumbnailImage?.FileName),
            item.MetaTitle, item.MetaKeywords, item.MetaDescription, item.PublishedOn));
    }

    // ----- Contact ---------------------------------------------------------------------------------

    [HttpGet("contact/areas")]
    public async Task<ActionResult<IReadOnlyList<ContactAreaPublicDto>>> ContactAreas(CancellationToken cancellationToken)
    {
        var areas = await _db.ContactAreas
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Name)
            .Select(a => new ContactAreaPublicDto(a.Id, a.Name))
            .ToListAsync(cancellationToken);

        return Ok(areas);
    }

    [HttpPost("contact")]
    public async Task<IActionResult> SubmitContact(SubmitContactRequest request, CancellationToken cancellationToken)
    {
        var areaExists = await _db.ContactAreas.AnyAsync(
            a => a.Id == request.ContactAreaId && !a.IsDeleted, cancellationToken);
        if (!areaExists)
        {
            return BadRequest(new { error = "The contact area does not exist." });
        }

        _db.Contacts.Add(new Contact
        {
            FullName = request.FullName,
            EmailAddress = request.EmailAddress,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Content = request.Content,
            ContactAreaId = request.ContactAreaId,
            CreatedOn = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
