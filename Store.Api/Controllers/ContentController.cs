using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Common;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>
/// Public content: CMS pages by slug (old Cms module), news listing/detail (old News module)
/// and the contact form (old Contacts module).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api")]
public sealed class ContentController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;
    private readonly ILocalizationService _localization;

    public ContentController(
        StoreDbContext db,
        TimeProvider timeProvider,
        IMediaUrlBuilder mediaUrl,
        ILocalizationService localization)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
        _localization = localization;
    }

    // ----- CMS pages ----------------------------------------------------------------------------

    [HttpGet("pages/{slug}")]
    public async Task<ActionResult<PublicPageDto>> Page(string slug, CancellationToken cancellationToken)
    {
        var page = await _db.Pages
            .Where(p => p.Slug == slug && p.IsPublished && !p.IsDeleted)
            .Select(p => new { p.Id, p.Name, p.Slug, p.Body, p.MetaTitle, p.MetaKeywords, p.MetaDescription })
            .FirstOrDefaultAsync(cancellationToken);

        if (page == null)
        {
            return NotFound();
        }

        var cultureId = RequestCulture.OverlayCultureId(Request);
        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.Page, new[] { page.Id }, cultureId, cancellationToken);

        return Ok(new PublicPageDto(
            overlay.Apply(page.Id, LocalizedProperty.Name, page.Name) ?? page.Name,
            page.Slug,
            overlay.Apply(page.Id, LocalizedProperty.Body, page.Body),
            page.MetaTitle, page.MetaKeywords, page.MetaDescription));
    }

    // ----- News ----------------------------------------------------------------------------------

    [HttpGet("news")]
    public async Task<ActionResult<IReadOnlyList<NewsListItemDto>>> News(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.NewsItems.Where(n => n.IsPublished && !n.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(n => n.Categories.Any(c => c.Slug == category));
        }

        var items = await query
            .OrderByDescending(n => n.PublishedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NewsListItemDto(
                n.Id, n.Name, n.Slug, n.ShortContent,
                n.ThumbnailImage != null ? n.ThumbnailImage.FileName : null, n.PublishedOn,
                n.Categories.OrderBy(c => c.DisplayOrder).Select(c => c.Slug).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.NewsItem, items.Select(i => i.Id).ToList(), cultureId, cancellationToken);

        return Ok(items.Select(i => i with
        {
            Name = overlay.Apply(i.Id, LocalizedProperty.Name, i.Name) ?? i.Name,
            ShortContent = overlay.Apply(i.Id, LocalizedProperty.ShortContent, i.ShortContent),
            ThumbnailUrl = _mediaUrl.GetUrl(i.ThumbnailUrl),
        }).ToList());
    }

    [HttpGet("news/{slug}")]
    public async Task<ActionResult<NewsDetailDto>> NewsDetail(string slug, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems
            .Include(n => n.ThumbnailImage)
            .Include(n => n.Categories)
            .Include(n => n.Product).ThenInclude(p => p!.ThumbnailImage)
            .FirstOrDefaultAsync(n => n.Slug == slug && n.IsPublished && !n.IsDeleted, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        var cultureId = RequestCulture.OverlayCultureId(Request);
        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.NewsItem, new[] { item.Id }, cultureId, cancellationToken);

        var categorySlug = item.Categories.OrderBy(c => c.DisplayOrder).Select(c => c.Slug).FirstOrDefault();

        NewsLinkedProductDto? product = null;
        // Only surface the linked product when it is still publicly visible.
        if (item.Product is { IsPublished: true, IsDeleted: false } p)
        {
            var productOverlay = await _localization.GetOverlayAsync(
                LocalizedEntity.Product, new[] { p.Id }, cultureId, cancellationToken);
            product = new NewsLinkedProductDto(
                p.Id,
                productOverlay.Apply(p.Id, LocalizedProperty.Name, p.Name) ?? p.Name,
                p.Slug, p.Price, _mediaUrl.GetUrl(p.ThumbnailImage?.FileName));
        }

        return Ok(new NewsDetailDto(
            item.Id,
            overlay.Apply(item.Id, LocalizedProperty.Name, item.Name) ?? item.Name,
            item.Slug,
            overlay.Apply(item.Id, LocalizedProperty.ShortContent, item.ShortContent),
            overlay.Apply(item.Id, LocalizedProperty.FullContent, item.FullContent),
            _mediaUrl.GetUrl(item.ThumbnailImage?.FileName),
            item.MetaTitle, item.MetaKeywords, item.MetaDescription, item.PublishedOn,
            categorySlug, product));
    }

    /// <summary>
    /// Published <c>alert</c>-category news for the home announcement band: not expired
    /// (<c>AlertExpiresOn</c> null or in the future), newest first, capped at 3.
    /// Anonymous and cheap; the storefront polls it and renders nothing when empty.
    /// </summary>
    [HttpGet("home/alerts")]
    public async Task<ActionResult<IReadOnlyList<AlertDto>>> HomeAlerts(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var items = await _db.NewsItems
            .Where(n => n.IsPublished && !n.IsDeleted
                && n.Categories.Any(c => c.Slug == NewsCategorySlugs.Alert)
                && (n.AlertExpiresOn == null || n.AlertExpiresOn > now))
            .OrderByDescending(n => n.PublishedOn)
            .Take(3)
            .Select(n => new AlertDto(n.Id, n.Slug, n.Name, n.ShortContent, n.AlertCtaUrl))
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.NewsItem, items.Select(i => i.Id).ToList(), cultureId, cancellationToken);

        return Ok(items.Select(i => i with
        {
            Name = overlay.Apply(i.Id, LocalizedProperty.Name, i.Name) ?? i.Name,
            ShortContent = overlay.Apply(i.Id, LocalizedProperty.ShortContent, i.ShortContent),
        }).ToList());
    }

    // ----- Content blocks (editable static text/media, fixed design) -------------------------------

    /// <summary>
    /// Active content blocks for a storefront page, with each block's <c>Value</c> overlaid to the
    /// requested culture and image blocks resolved to a <c>/user-content/…</c> URL. Anonymous and
    /// cacheable; the storefront renders these over hard-coded fallbacks.
    /// </summary>
    [HttpGet("content/blocks/{pageKey}")]
    public async Task<ActionResult<IReadOnlyList<PublicContentBlockDto>>> ContentBlocks(
        string pageKey, CancellationToken cancellationToken)
    {
        var blocks = await _db.ContentBlocks
            .Where(b => b.PageKey == pageKey && b.IsActive)
            .OrderBy(b => b.SectionKey).ThenBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new
            {
                b.Id,
                b.SectionKey,
                b.BlockKey,
                b.Type,
                b.Value,
                b.LinkUrl,
                MediumFileName = b.Medium != null ? b.Medium.FileName : null,
            })
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.ContentBlock, blocks.Select(b => b.Id).ToList(), cultureId, cancellationToken);

        var result = blocks
            .Select(b => new PublicContentBlockDto(
                b.SectionKey,
                b.BlockKey,
                b.Type,
                overlay.Apply(b.Id, LocalizedProperty.Value, b.Value),
                _mediaUrl.GetUrl(b.MediumFileName),
                b.LinkUrl))
            .ToList();

        return Ok(result);
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

/// <summary>One active content block for the storefront (value culture-overlaid, media resolved).</summary>
public sealed record PublicContentBlockDto(
    string SectionKey, string BlockKey, string Type, string? Value, string? MediaUrl, string? LinkUrl);
