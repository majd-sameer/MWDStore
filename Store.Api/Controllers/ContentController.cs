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
            .Select(p => new PublicPageDto(p.Name, p.Slug, p.Body, p.MetaTitle, p.MetaKeywords, p.MetaDescription))
            .FirstOrDefaultAsync(cancellationToken);

        return page == null ? NotFound() : Ok(page);
    }

    // ----- News ----------------------------------------------------------------------------------

    [HttpGet("news")]
    public async Task<ActionResult<IReadOnlyList<NewsListItemDto>>> News(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var items = await _db.NewsItems
            .Where(n => n.IsPublished && !n.IsDeleted)
            .OrderByDescending(n => n.PublishedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NewsListItemDto(
                n.Id, n.Name, n.Slug, n.ShortContent,
                n.ThumbnailImage != null ? n.ThumbnailImage.FileName : null, n.PublishedOn))
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
            .FirstOrDefaultAsync(n => n.Slug == slug && n.IsPublished && !n.IsDeleted, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        var cultureId = RequestCulture.OverlayCultureId(Request);
        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.NewsItem, new[] { item.Id }, cultureId, cancellationToken);

        return Ok(new NewsDetailDto(
            item.Id,
            overlay.Apply(item.Id, LocalizedProperty.Name, item.Name) ?? item.Name,
            item.Slug,
            overlay.Apply(item.Id, LocalizedProperty.ShortContent, item.ShortContent),
            overlay.Apply(item.Id, LocalizedProperty.FullContent, item.FullContent),
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
