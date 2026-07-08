using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin CRUD for CMS pages (old Cms module's page admin). Deletes are soft. <c>Name</c>, <c>Body</c>
/// and the meta fields are bilingual: Arabic in the base columns, English in the
/// <c>LocalizedContentProperty</c> overlay (served to the storefront under <c>Accept-Language: en</c>).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/pages")]
public sealed class AdminPagesController : ControllerBase
{
    private const string EntityType = LocalizedEntity.Page;
    private static readonly string EnCulture = RequestCulture.EnglishCultureId;

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;

    public AdminPagesController(
        StoreDbContext db, TimeProvider timeProvider,
        ILocalizationService localization, ILocalizedContentWriter localizedWriter)
    {
        _db = db;
        _timeProvider = timeProvider;
        _localization = localization;
        _localizedWriter = localizedWriter;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPageDto>>> List(CancellationToken cancellationToken)
    {
        var pages = await _db.Pages
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayAsync(
            EntityType, pages.Select(p => p.Id).ToList(), EnCulture, cancellationToken);

        return Ok(pages.Select(p => ToDto(p, overlay)).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminPageDto>> Get(long id, CancellationToken cancellationToken)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        var overlay = await _localization.GetOverlayAsync(EntityType, new[] { id }, EnCulture, cancellationToken);
        return Ok(ToDto(page, overlay));
    }

    [HttpPost]
    public async Task<ActionResult<AdminPageDto>> Create(PageUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();
        var page = new Page
        {
            CreatedById = userId,
            CreatedOn = now,
            LatestUpdatedById = userId,
            LatestUpdatedOn = now
        };
        Apply(page, request, now);

        if (await _db.Pages.AnyAsync(p => p.Slug == page.Slug && !p.IsDeleted, cancellationToken))
        {
            return Conflict(new { error = $"A page with slug '{page.Slug}' already exists." });
        }

        _db.Pages.Add(page);
        await _db.SaveChangesAsync(cancellationToken);

        await WriteEnglishAsync(page.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = page.Id }, ToDto(page, request));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminPageDto>> Update(
        long id, PageUpsertRequest request, CancellationToken cancellationToken)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        var now = _timeProvider.GetUtcNow();
        Apply(page, request, now);
        page.LatestUpdatedById = User.GetUserId();
        page.LatestUpdatedOn = now;

        if (await _db.Pages.AnyAsync(p => p.Slug == page.Slug && p.Id != id && !p.IsDeleted, cancellationToken))
        {
            return Conflict(new { error = $"A page with slug '{page.Slug}' already exists." });
        }

        await WriteEnglishAsync(page.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(page, request));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (page == null)
        {
            return NotFound();
        }

        page.IsDeleted = true;
        page.LatestUpdatedById = User.GetUserId();
        page.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private void Apply(Page page, PageUpsertRequest request, DateTimeOffset now)
    {
        page.Name = request.Name;
        page.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        page.Body = request.Body;
        page.MetaTitle = request.MetaTitle;
        page.MetaKeywords = request.MetaKeywords;
        page.MetaDescription = request.MetaDescription;
        page.IsPublished = request.IsPublished;
        page.PublishedOn ??= request.IsPublished ? now : null;
    }

    private async Task WriteEnglishAsync(long id, PageUpsertRequest request, CancellationToken cancellationToken)
    {
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.Body, EnCulture, request.BodyEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.MetaTitle, EnCulture, request.MetaTitleEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.MetaKeywords, EnCulture, request.MetaKeywordsEn, cancellationToken);
        await _localizedWriter.SetAsync(EntityType, id, LocalizedProperty.MetaDescription, EnCulture, request.MetaDescriptionEn, cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Builds the DTO from a loaded page + an overlay read from the DB.</summary>
    private static AdminPageDto ToDto(Page p, LocalizedOverlay overlay) => new(
        p.Id, p.Name, overlay.Get(p.Id, LocalizedProperty.Name), p.Slug,
        p.Body, overlay.Get(p.Id, LocalizedProperty.Body),
        p.MetaTitle, overlay.Get(p.Id, LocalizedProperty.MetaTitle),
        p.MetaKeywords, overlay.Get(p.Id, LocalizedProperty.MetaKeywords),
        p.MetaDescription, overlay.Get(p.Id, LocalizedProperty.MetaDescription),
        p.IsPublished, p.PublishedOn, p.CreatedOn);

    /// <summary>Builds the DTO straight from a just-saved request (English values echoed back).</summary>
    private static AdminPageDto ToDto(Page p, PageUpsertRequest request) => new(
        p.Id, p.Name, Normalize(request.NameEn), p.Slug,
        p.Body, Normalize(request.BodyEn),
        p.MetaTitle, Normalize(request.MetaTitleEn),
        p.MetaKeywords, Normalize(request.MetaKeywordsEn),
        p.MetaDescription, Normalize(request.MetaDescriptionEn),
        p.IsPublished, p.PublishedOn, p.CreatedOn);
}
