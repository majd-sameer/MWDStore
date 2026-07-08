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
    private readonly ILocalizationService _localization;

    public CatalogController(
        ICatalogService catalog,
        StoreDbContext db,
        TimeProvider timeProvider,
        ILocalizationService localization)
    {
        _catalog = catalog;
        _db = db;
        _timeProvider = timeProvider;
        _localization = localization;
    }

    /// <summary>Overlays English product fields onto a listing when the request asks for English.</summary>
    private Task LocalizeAsync(ProductListResult result, CancellationToken cancellationToken) =>
        LocalizeProductsAsync(result.Products, cancellationToken);

    /// <summary>Overlays English name/short-description onto a set of list items when English is requested.</summary>
    private async Task LocalizeProductsAsync(
        IList<ProductListItem> products, CancellationToken cancellationToken)
    {
        var cultureId = RequestCulture.OverlayCultureId(Request);
        if (cultureId is null || products.Count == 0)
        {
            return;
        }

        var ids = products.Select(p => p.Id).ToList();
        var overlay = await _localization.GetOverlayAsync(LocalizedEntity.Product, ids, cultureId, cancellationToken);
        if (overlay.IsEmpty)
        {
            return;
        }

        foreach (var product in products)
        {
            product.Name = overlay.Apply(product.Id, LocalizedProperty.Name, product.Name) ?? product.Name;
            product.ShortDescription = overlay.Apply(product.Id, LocalizedProperty.ShortDescription, product.ShortDescription);
        }
    }

    /// <summary>Curated signature products for the home rail (published + in-catalog, sorted).</summary>
    [HttpGet("signature")]
    public async Task<ActionResult<IList<ProductListItem>>> Signature(
        [FromQuery] int take = 8, CancellationToken cancellationToken = default)
    {
        var products = await _catalog.GetSignatureProductsAsync(take, cancellationToken);
        await LocalizeProductsAsync(products, cancellationToken);
        return Ok(products);
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

        await LocalizeAsync(result, cancellationToken);
        return Ok(result);
    }

    /// <summary>Products within a category.</summary>
    [HttpGet("categories/{categoryId:long}/products")]
    public async Task<ActionResult<ProductListResult>> ByCategory(
        long categoryId, [FromQuery] ProductListOptions options, CancellationToken cancellationToken)
    {
        var result = await _catalog.GetProductsByCategoryAsync(categoryId, options, cancellationToken);
        await LocalizeAsync(result, cancellationToken);
        return Ok(result);
    }

    /// <summary>Full product detail (attributes, categories, variations, related products).</summary>
    [HttpGet("products/{id:long}")]
    public async Task<ActionResult<ProductDetailModel>> ProductDetail(long id, CancellationToken cancellationToken)
    {
        var product = await _catalog.GetProductDetailAsync(id, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        var cultureId = RequestCulture.OverlayCultureId(Request);
        if (cultureId is not null)
        {
            var ids = new List<long>(product.RelatedProducts.Count + 1) { product.Id };
            ids.AddRange(product.RelatedProducts.Select(r => r.Id));
            var overlay = await _localization.GetOverlayAsync(LocalizedEntity.Product, ids, cultureId, cancellationToken);

            if (!overlay.IsEmpty)
            {
                product.Name = overlay.Apply(product.Id, LocalizedProperty.Name, product.Name) ?? product.Name;
                product.ShortDescription = overlay.Apply(product.Id, LocalizedProperty.ShortDescription, product.ShortDescription);
                product.Description = overlay.Apply(product.Id, LocalizedProperty.Description, product.Description);
                product.Specification = overlay.Apply(product.Id, LocalizedProperty.Specification, product.Specification);

                foreach (var related in product.RelatedProducts)
                {
                    related.Name = overlay.Apply(related.Id, LocalizedProperty.Name, related.Name) ?? related.Name;
                    related.ShortDescription = overlay.Apply(related.Id, LocalizedProperty.ShortDescription, related.ShortDescription);
                }
            }
        }

        return Ok(product);
    }

    /// <summary>Published categories (flattened tree).</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories(CancellationToken cancellationToken)
    {
        var categories = await _db.Categories
            .Where(c => c.IsPublished && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.DisplayOrder, c.IncludeInMenu))
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        if (cultureId is not null && categories.Count > 0)
        {
            var overlay = await _localization.GetOverlayAsync(
                LocalizedEntity.Category, categories.Select(c => c.Id).ToList(), cultureId, cancellationToken);
            if (!overlay.IsEmpty)
            {
                categories = categories
                    .Select(c => c with { Name = overlay.Apply(c.Id, LocalizedProperty.Name, c.Name)! })
                    .ToList();
            }
        }

        return Ok(categories);
    }

    /// <summary>Published brands.</summary>
    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> Brands(CancellationToken cancellationToken)
    {
        var brands = await _db.Brands
            .Where(b => b.IsPublished && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.Slug))
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        if (cultureId is not null && brands.Count > 0)
        {
            var overlay = await _localization.GetOverlayAsync(
                LocalizedEntity.Brand, brands.Select(b => b.Id).ToList(), cultureId, cancellationToken);
            if (!overlay.IsEmpty)
            {
                brands = brands
                    .Select(b => b with { Name = overlay.Apply(b.Id, LocalizedProperty.Name, b.Name)! })
                    .ToList();
            }
        }

        return Ok(brands);
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
