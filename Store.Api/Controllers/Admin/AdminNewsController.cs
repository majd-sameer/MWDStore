using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin CRUD for news categories and news items. Deletes are soft. Human-readable copy
/// (category Name/Description; item Name/ShortContent/FullContent/meta) is bilingual: Arabic in
/// the base columns, English in the <c>LocalizedContentProperty</c> overlay.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/news")]
public sealed class AdminNewsController : ControllerBase
{

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaStorage _mediaStorage;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;
    private readonly IAuditStampReader _auditStamps;

    public AdminNewsController(
        StoreDbContext db, TimeProvider timeProvider, IMediaStorage mediaStorage,
        ILocalizationService localization, ILocalizedContentWriter localizedWriter,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaStorage = mediaStorage;
        _localization = localization;
        _localizedWriter = localizedWriter;
        _auditStamps = auditStamps;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<AdminNewsCategoryDto>>> ListCategories(CancellationToken cancellationToken)
    {
        var categories = await _db.NewsCategories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new { c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder, c.IsPublished })
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.NewsCategory, categories.Select(c => c.Id).ToList(), RequestCulture.EnglishCultureId, cancellationToken);

        var dtos = categories
            .Select(c => new AdminNewsCategoryDto(
                c.Id, c.Name, overlay.Get(c.Id, LocalizedProperty.Name), c.Slug,
                c.Description, overlay.Get(c.Id, LocalizedProperty.Description), c.DisplayOrder, c.IsPublished))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("categories")]
    public async Task<ActionResult<AdminNewsCategoryDto>> CreateCategory(
        NewsCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = new NewsCategory();
        ApplyCategory(category, request);
        _db.NewsCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        await WriteCategoryEnglishAsync(category.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(CategoryDto(category, request));
    }

    [HttpPut("categories/{id:long}")]
    public async Task<ActionResult<AdminNewsCategoryDto>> UpdateCategory(
        long id, NewsCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = await _db.NewsCategories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        ApplyCategory(category, request);
        await WriteCategoryEnglishAsync(category.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(CategoryDto(category, request));
    }

    [HttpDelete("categories/{id:long}")]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken cancellationToken)
    {
        var category = await _db.NewsCategories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (category == null)
        {
            return NotFound();
        }

        category.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void ApplyCategory(NewsCategory category, NewsCategoryUpsertRequest request)
    {
        category.Name = request.Name;
        category.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        category.Description = request.Description;
        category.DisplayOrder = request.DisplayOrder;
        category.IsPublished = request.IsPublished;
    }

    private async Task WriteCategoryEnglishAsync(long id, NewsCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        await _localizedWriter.SetAsync(LocalizedEntity.NewsCategory, id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _localizedWriter.SetAsync(LocalizedEntity.NewsCategory, id, LocalizedProperty.Description, RequestCulture.EnglishCultureId, request.DescriptionEn, cancellationToken);
    }

    private static AdminNewsCategoryDto CategoryDto(NewsCategory c, NewsCategoryUpsertRequest request) => new(
        c.Id, c.Name, AdminText.NormalizeOrNull(request.NameEn), c.Slug, c.Description, AdminText.NormalizeOrNull(request.DescriptionEn),
        c.DisplayOrder, c.IsPublished);

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<AdminNewsItemListItem>>> ListItems(CancellationToken cancellationToken)
    {
        var items = await _db.NewsItems
            .Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.Id)
            .Select(n => new AdminNewsItemListItem(
                n.Id, n.Name, n.Slug, n.IsPublished, n.CreatedOn,
                n.ThumbnailImage != null ? n.ThumbnailImage.FileName : null))
            .ToListAsync(cancellationToken);

        return Ok(await items.WithAuditStampsAsync(
            _auditStamps, nameof(NewsItem), i => i.Id,
            (i, createdBy, modifiedBy) => i with
            {
                ThumbnailUrl = _mediaStorage.GetUrl(i.ThumbnailUrl),
                CreatedBy = createdBy,
                ModifiedBy = modifiedBy,
            },
            cancellationToken));
    }

    [HttpGet("items/{id:long}")]
    public async Task<ActionResult<AdminNewsItemDetail>> GetItem(long id, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems
            .AsNoTracking()
            .Include(n => n.Categories)
            .Include(n => n.ThumbnailImage)
            .Include(n => n.Product)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.NewsItem, new[] { id }, RequestCulture.EnglishCultureId, cancellationToken);
        return Ok(ToDetail(item, overlay));
    }

    [HttpPost("items")]
    public async Task<ActionResult<AdminNewsItemDetail>> CreateItem(
        NewsItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();
        var item = new NewsItem
        {
            CreatedById = userId,
            CreatedOn = now,
            LatestUpdatedById = userId,
            LatestUpdatedOn = now
        };
        ApplyItem(item, request, now);
        await SetCategoriesAsync(item, request.CategoryIds, cancellationToken);

        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        await WriteItemEnglishAsync(item.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, ToDetail(item, request));
    }

    [HttpPut("items/{id:long}")]
    public async Task<ActionResult<AdminNewsItemDetail>> UpdateItem(
        long id, NewsItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems
            .Include(n => n.Categories)
            .Include(n => n.ThumbnailImage)
            .Include(n => n.Product)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        var now = _timeProvider.GetUtcNow();
        ApplyItem(item, request, now);
        item.LatestUpdatedById = User.GetUserId();
        item.LatestUpdatedOn = now;
        await SetCategoriesAsync(item, request.CategoryIds, cancellationToken);

        await WriteItemEnglishAsync(item.Id, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDetail(item, request));
    }

    [HttpDelete("items/{id:long}")]
    public async Task<IActionResult> DeleteItem(long id, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        item.IsDeleted = true;
        item.LatestUpdatedById = User.GetUserId();
        item.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void ApplyItem(NewsItem item, NewsItemUpsertRequest request, DateTimeOffset now)
    {
        item.Name = request.Name;
        item.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        item.ShortContent = request.ShortContent;
        item.FullContent = request.FullContent;
        item.MetaTitle = request.MetaTitle;
        item.MetaKeywords = request.MetaKeywords;
        item.MetaDescription = request.MetaDescription;
        item.IsPublished = request.IsPublished;
        item.PublishedOn ??= request.IsPublished ? now : null;
        item.ThumbnailImageId = request.ThumbnailImageId;
        item.ProductId = request.ProductId;
        item.AlertExpiresOn = request.AlertExpiresOn;
        item.AlertCtaUrl = string.IsNullOrWhiteSpace(request.AlertCtaUrl) ? null : request.AlertCtaUrl.Trim();
    }

    private async Task WriteItemEnglishAsync(long id, NewsItemUpsertRequest request, CancellationToken cancellationToken)
    {
        await _localizedWriter.SetAsync(LocalizedEntity.NewsItem, id, LocalizedProperty.Name, RequestCulture.EnglishCultureId, request.NameEn, cancellationToken);
        await _localizedWriter.SetAsync(LocalizedEntity.NewsItem, id, LocalizedProperty.ShortContent, RequestCulture.EnglishCultureId, request.ShortContentEn, cancellationToken);
        await _localizedWriter.SetAsync(LocalizedEntity.NewsItem, id, LocalizedProperty.FullContent, RequestCulture.EnglishCultureId, request.FullContentEn, cancellationToken);
        await _localizedWriter.SetAsync(LocalizedEntity.NewsItem, id, LocalizedProperty.MetaTitle, RequestCulture.EnglishCultureId, request.MetaTitleEn, cancellationToken);
        await _localizedWriter.SetAsync(LocalizedEntity.NewsItem, id, LocalizedProperty.MetaKeywords, RequestCulture.EnglishCultureId, request.MetaKeywordsEn, cancellationToken);
        await _localizedWriter.SetAsync(LocalizedEntity.NewsItem, id, LocalizedProperty.MetaDescription, RequestCulture.EnglishCultureId, request.MetaDescriptionEn, cancellationToken);
    }

    private async Task SetCategoriesAsync(NewsItem item, IList<long> categoryIds, CancellationToken cancellationToken)
    {
        var categories = await _db.NewsCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        item.Categories.Clear();
        foreach (var category in categories)
        {
            item.Categories.Add(category);
        }
    }

    /// <summary>Builds the detail from a loaded item + an overlay read from the DB.</summary>
    private AdminNewsItemDetail ToDetail(NewsItem n, LocalizedOverlay overlay) => new(
        n.Id, n.Name, overlay.Get(n.Id, LocalizedProperty.Name), n.Slug,
        n.ShortContent, overlay.Get(n.Id, LocalizedProperty.ShortContent),
        n.FullContent, overlay.Get(n.Id, LocalizedProperty.FullContent),
        n.MetaTitle, overlay.Get(n.Id, LocalizedProperty.MetaTitle),
        n.MetaKeywords, overlay.Get(n.Id, LocalizedProperty.MetaKeywords),
        n.MetaDescription, overlay.Get(n.Id, LocalizedProperty.MetaDescription),
        n.IsPublished, n.ThumbnailImageId, _mediaStorage.GetUrl(n.ThumbnailImage?.FileName),
        n.Categories.Select(c => c.Id).ToList(),
        n.ProductId, n.Product?.Name, n.AlertExpiresOn, n.AlertCtaUrl);

    /// <summary>Builds the detail straight from a just-saved request (English values echoed back).</summary>
    private AdminNewsItemDetail ToDetail(NewsItem n, NewsItemUpsertRequest request) => new(
        n.Id, n.Name, AdminText.NormalizeOrNull(request.NameEn), n.Slug,
        n.ShortContent, AdminText.NormalizeOrNull(request.ShortContentEn),
        n.FullContent, AdminText.NormalizeOrNull(request.FullContentEn),
        n.MetaTitle, AdminText.NormalizeOrNull(request.MetaTitleEn),
        n.MetaKeywords, AdminText.NormalizeOrNull(request.MetaKeywordsEn),
        n.MetaDescription, AdminText.NormalizeOrNull(request.MetaDescriptionEn),
        n.IsPublished, n.ThumbnailImageId, _mediaStorage.GetUrl(n.ThumbnailImage?.FileName),
        n.Categories.Select(c => c.Id).ToList(),
        n.ProductId, n.Product?.Name, n.AlertExpiresOn, n.AlertCtaUrl);
}
