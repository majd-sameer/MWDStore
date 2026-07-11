using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Catalog;
using Store.Application.Catalog.Models;
using Store.Application.Localization;
using Store.Data;

namespace Store.Api.Controllers;

/// <summary>Public storefront catalog: product search, category listings, product detail, categories and brands.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IRequestCulture _culture;

    public CatalogController(
        ICatalogService catalog,
        StoreDbContext db,
        TimeProvider timeProvider,
        IRequestCulture culture)
    {
        _catalog = catalog;
        _db = db;
        _timeProvider = timeProvider;
        _culture = culture;
    }

    /// <summary>Search/browse products with optional query, brand/category facets, price range, sort and paging.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<ProductListResult>> Search(
        [FromQuery] ProductListOptions options, CancellationToken cancellationToken)
    {
        var result = await _catalog.SearchAsync(options, cancellationToken);

        // Old Search module behavior: persist what customers search for (feeds the admin query log).
        if (!string.IsNullOrWhiteSpace(options.Query))
        {
            _db.Queries.Add(new Store.Domain.Query
            {
                QueryText = options.Query.Trim(),
                ResultsCount = result.TotalProduct,
                CreatedOn = _timeProvider.GetUtcNow()
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(result);
    }

    /// <summary>Products within a category.</summary>
    [HttpGet("categories/{categoryId:long}/products")]
    public async Task<ActionResult<ProductListResult>> ByCategory(
        long categoryId, [FromQuery] ProductListOptions options, CancellationToken cancellationToken)
    {
        var result = await _catalog.GetProductsByCategoryAsync(categoryId, options, cancellationToken);
        return Ok(result);
    }

    /// <summary>Full product detail (attributes, categories, variations, related products).
    /// All localized text is resolved to the request language inside <see cref="ICatalogService"/>.</summary>
    [HttpGet("products/{id:long}")]
    public async Task<ActionResult<ProductDetailModel>> ProductDetail(long id, CancellationToken cancellationToken)
    {
        var product = await _catalog.GetProductDetailAsync(id, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    /// <summary>Published categories (flattened tree). Names resolve per request culture.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories(CancellationToken cancellationToken)
    {
        var rows = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsPublished && !c.IsDeleted)
            .Select(c => new { c.Id, c.Name, c.Slug, c.ParentId, c.DisplayOrder, c.IncludeInMenu })
            .ToListAsync(cancellationToken);

        var lang = _culture.Language;
        return Ok(rows
            .OrderBy(r => r.DisplayOrder).ThenBy(r => r.Name.Ar)
            .Select(r => new CategoryDto(r.Id, r.Name.Resolve(lang)!, r.Slug, r.ParentId, r.DisplayOrder, r.IncludeInMenu))
            .ToList());
    }

    /// <summary>Published brands. Names resolve per request culture.</summary>
    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> Brands(CancellationToken cancellationToken)
    {
        var rows = await _db.Brands
            .AsNoTracking()
            .Where(b => b.IsPublished && !b.IsDeleted)
            .Select(b => new { b.Id, b.Name, b.Slug })
            .ToListAsync(cancellationToken);

        var lang = _culture.Language;
        return Ok(rows
            .OrderBy(r => r.Name.Ar)
            .Select(r => new BrandDto(r.Id, r.Name.Resolve(lang)!, r.Slug))
            .ToList());
    }

    /// <summary>Count of active vendors (reform &amp; rehabilitation centers), for the home hero stat.</summary>
    [HttpGet("vendors/count")]
    public async Task<ActionResult<int>> VendorCount(CancellationToken cancellationToken)
    {
        var count = await _db.Vendors
            .CountAsync(v => v.IsActive && !v.IsDeleted, cancellationToken);

        return Ok(count);
    }
}
