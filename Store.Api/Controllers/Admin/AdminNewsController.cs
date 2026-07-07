using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for news categories and news items (old News module). Deletes are soft.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Content)]
[Route("api/admin/news")]
public sealed class AdminNewsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaStorage _mediaStorage;

    public AdminNewsController(StoreDbContext db, TimeProvider timeProvider, IMediaStorage mediaStorage)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaStorage = mediaStorage;
    }

    // ----- Categories -----------------------------------------------------------------------------

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<AdminNewsCategoryDto>>> ListCategories(CancellationToken cancellationToken)
    {
        var categories = await _db.NewsCategories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new AdminNewsCategoryDto(c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder, c.IsPublished))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpPost("categories")]
    public async Task<ActionResult<AdminNewsCategoryDto>> CreateCategory(
        NewsCategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var category = new NewsCategory();
        ApplyCategory(category, request);
        _db.NewsCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminNewsCategoryDto(
            category.Id, category.Name, category.Slug, category.Description, category.DisplayOrder, category.IsPublished));
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
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminNewsCategoryDto(
            category.Id, category.Name, category.Slug, category.Description, category.DisplayOrder, category.IsPublished));
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

    // ----- News items ------------------------------------------------------------------------------

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

        return Ok(items.Select(i => i with { ThumbnailUrl = _mediaStorage.GetUrl(i.ThumbnailUrl) }).ToList());
    }

    [HttpGet("items/{id:long}")]
    public async Task<ActionResult<AdminNewsItemDetail>> GetItem(long id, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems
            .Include(n => n.Categories)
            .Include(n => n.ThumbnailImage)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

        return item == null ? NotFound() : Ok(ToDetail(item));
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

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, ToDetail(item));
    }

    [HttpPut("items/{id:long}")]
    public async Task<ActionResult<AdminNewsItemDetail>> UpdateItem(
        long id, NewsItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.NewsItems
            .Include(n => n.Categories)
            .Include(n => n.ThumbnailImage)
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
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDetail(item));
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

    private AdminNewsItemDetail ToDetail(NewsItem n) => new(
        n.Id, n.Name, n.Slug, n.ShortContent, n.FullContent,
        n.MetaTitle, n.MetaKeywords, n.MetaDescription,
        n.IsPublished, n.ThumbnailImageId, _mediaStorage.GetUrl(n.ThumbnailImage?.FileName),
        n.Categories.Select(c => c.Id).ToList());
}
