using Microsoft.EntityFrameworkCore;
using Store.Application.Catalog.Models;
using Store.Application.Catalog.Pricing;
using Store.Application.Common;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Application.Catalog;

/// <summary>
/// Faithful port of SimplCommerce's storefront catalog read paths:
/// <c>CategoryController.CategoryDetail</c>, <c>SearchController.Index</c> and
/// <c>ProductController.ProductDetail</c>. Media, product-level localization, currency formatting
/// and the search-query persistence / brand-redirect concerns are intentionally out of scope (the
/// caller overlays the product's own Name/Description fields) — this service resolves the
/// option/attribute/brand/category names baked into the listing and product-detail payloads via
/// their own <see cref="LocalizedString"/> (<see cref="IRequestCulture"/>).
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private readonly StoreDbContext _db;
    private readonly IProductPricingService _pricing;
    private readonly CatalogOptions _options;
    private readonly IMediaUrlBuilder _mediaUrl;
    private readonly IRequestCulture _culture;

    public CatalogService(
        StoreDbContext db, IProductPricingService pricing, CatalogOptions options, IMediaUrlBuilder mediaUrl,
        IRequestCulture culture)
    {
        _db = db;
        _pricing = pricing;
        _options = options;
        _mediaUrl = mediaUrl;
        _culture = culture;
    }

    public async Task<ProductListResult> GetProductsByCategoryAsync(
        long categoryId, ProductListOptions options, CancellationToken cancellationToken = default)
    {
        var baseQuery = _db.Products.Where(x =>
            x.ProductCategories.Any(c => c.CategoryId == categoryId)
            && x.IsPublished
            && x.IsVisibleIndividually);

        return await BuildListResultAsync(baseQuery, options, cancellationToken);
    }

    public async Task<ProductListResult> SearchAsync(
        ProductListOptions options, CancellationToken cancellationToken = default)
    {
        // SimplCommerce only applies the category filter when Category != "all".
        if (string.Equals(options.Category, "all", StringComparison.OrdinalIgnoreCase))
        {
            options.Category = null;
        }

        var baseQuery = _db.Products.Where(x => x.IsPublished && x.IsVisibleIndividually);

        // Unlike SimplCommerce (which redirects to home), an empty query browses the full catalog —
        // the storefront listing page uses this endpoint with filters but no search text.
        if (!string.IsNullOrWhiteSpace(options.Query))
        {
            var q = options.Query.Trim().ToLower();

            // Note: the null-guards reproduce SQL NULL semantics (LOWER(NULL) LIKE '%q%' is NULL/false) and
            // are required for client-side (LINQ-to-objects) evaluation; the matched set is identical.
            // Match both the Arabic (base) and English overlay of each localized field so English-mode
            // shoppers can search by the English name/description (user-approved improvement).
            baseQuery = baseQuery.Where(x =>
                (x.Name.Ar != null && x.Name.Ar.ToLower().Contains(q))
                || (x.Name.En != null && x.Name.En.ToLower().Contains(q))
                || (x.ShortDescription!.Ar != null && x.ShortDescription.Ar.ToLower().Contains(q))
                || (x.ShortDescription!.En != null && x.ShortDescription.En.ToLower().Contains(q))
                || (x.Description!.Ar != null && x.Description.Ar.ToLower().Contains(q))
                || (x.Description!.En != null && x.Description.En.ToLower().Contains(q))
                || (x.Specification != null && x.Specification.ToLower().Contains(q)));
        }

        return await BuildListResultAsync(baseQuery, options, cancellationToken);
    }

    /// <summary>
    /// The pipeline shared by the category and search listings (same order as SimplCommerce):
    /// facets over the base query → price/category/brand filters → count → page clamp → sort →
    /// page → project + resolve price.
    /// </summary>
    private async Task<ProductListResult> BuildListResultAsync(
        IQueryable<Product> baseQuery, ProductListOptions options, CancellationToken cancellationToken)
    {
        var result = new ProductListResult();

        if (!await baseQuery.AnyAsync(cancellationToken))
        {
            result.TotalProduct = 0;
            return result;
        }

        await AppendFilterOptionsAsync(result.FilterOption, baseQuery, _culture.Language, cancellationToken);

        var query = baseQuery;

        if (options.MinPrice.HasValue)
        {
            query = query.Where(x => x.Price >= options.MinPrice.Value);
        }

        if (options.MaxPrice.HasValue)
        {
            query = query.Where(x => x.Price <= options.MaxPrice.Value);
        }

        if (options.MinRating.HasValue)
        {
            query = query.Where(x => x.RatingAverage >= options.MinRating.Value);
        }

        var categories = options.GetCategories();
        if (categories.Any())
        {
            query = query.Where(x => x.ProductCategories.Any(c => categories.Contains(c.Category.Slug)));
        }

        var brands = options.GetBrands().ToArray();
        if (brands.Length > 0)
        {
            query = query.Where(x => x.BrandId != null && brands.Contains(x.Brand!.Slug));
        }

        // The client may ask for a (bounded) page size; otherwise the configured default applies.
        var pageSize = options.PageSize is > 0 and <= 48 ? options.PageSize : _options.ProductPageSize;
        var total = await query.CountAsync(cancellationToken);
        result.TotalProduct = total;

        var currentPageNum = options.Page <= 0 ? 1 : options.Page;
        var offset = (pageSize * currentPageNum) - pageSize;
        while (currentPageNum > 1 && offset >= total)
        {
            currentPageNum--;
            offset = (pageSize * currentPageNum) - pageSize;
        }

        query = ApplySort(options, query);

        var products = await query
            .Include(x => x.ThumbnailImage)
            .Include(x => x.ProductCategories).ThenInclude(c => c.Category)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(ToListItem).ToList();
        foreach (var item in items)
        {
            item.CalculatedProductPrice = _pricing.CalculateProductPrice(
                item.Price, item.OldPrice, item.SpecialPrice, item.SpecialPriceStart, item.SpecialPriceEnd);
        }

        result.Products = items;
        result.PageSize = pageSize;
        result.Page = currentPageNum;
        return result;
    }

    private static IQueryable<Product> ApplySort(ProductListOptions options, IQueryable<Product> query)
    {
        // In-stock (orderable) products always lead, whatever sort is chosen, so sold-out items
        // sink to the end of the whole result set rather than just within a page. Call-for-pricing
        // items count as available. This mirrors the storefront's own availability check (which only
        // sees IsAllowToOrder / StockQuantity / IsCallForPricing) so SSR and client agree and the
        // list doesn't reshuffle on hydration.
        var available = query.OrderByDescending(x =>
            x.IsCallForPricing || (x.IsAllowToOrder && x.StockQuantity > 0));

        var sortBy = options.Sort ?? string.Empty;
        return sortBy.ToLower() switch
        {
            "price-asc" => available.ThenBy(x => x.Price),
            "price-desc" => available.ThenByDescending(x => x.Price),
            // SQL Server sorts NULLs last in DESC order, so unrated products trail.
            "rating" => available.ThenByDescending(x => x.RatingAverage).ThenByDescending(x => x.ReviewsCount),
            "newest" => available.ThenByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id),
            // "featured" (default): featured products first, then stable catalog order.
            _ => available.ThenByDescending(x => x.IsFeatured).ThenBy(x => x.Id),
        };
    }

    private static async Task AppendFilterOptionsAsync(
        FilterOption filter, IQueryable<Product> baseQuery, ContentLanguage lang, CancellationToken cancellationToken)
    {
        filter.Total = await baseQuery.CountAsync(cancellationToken);
        filter.Price.MaxPrice = await baseQuery.MaxAsync(x => x.Price, cancellationToken);
        filter.Price.MinPrice = await baseQuery.MinAsync(x => x.Price, cancellationToken);

        // Project the owned Name members (not the owned instance itself) so the GroupBy/aggregate
        // translates to SQL, then resolve the display language in memory (facet labels localize).
        var categoryGroups = await baseQuery
            .SelectMany(x => x.ProductCategories)
            .GroupBy(x => new
            {
                x.Category.Id, x.Category.Slug, x.Category.ParentId,
                NameAr = x.Category.Name.Ar, NameEn = x.Category.Name.En
            })
            .Select(g => new { g.Key.Id, g.Key.Slug, g.Key.ParentId, g.Key.NameAr, g.Key.NameEn, Count = g.Count() })
            .ToListAsync(cancellationToken);

        filter.Categories = categoryGroups
            .Select(g => new FilterCategory
            {
                Id = g.Id,
                Name = new LocalizedString(g.NameAr, g.NameEn).Resolve(lang)!,
                Slug = g.Slug,
                ParentId = g.ParentId,
                Count = g.Count
            })
            .ToList();

        var brandProducts = await baseQuery
            .Where(x => x.BrandId != null)
            .Include(x => x.Brand)
            .ToListAsync(cancellationToken);

        filter.Brands = brandProducts
            .GroupBy(x => x.Brand!)
            .Select(g => new FilterBrand
            {
                Id = g.Key.Id,
                Name = g.Key.Name.Resolve(lang)!,
                Slug = g.Key.Slug,
                Count = g.Count()
            })
            .ToList();
    }

    public async Task<ProductDetailModel?> GetProductDetailAsync(
        long id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .Include(x => x.ProductOptionValues)
            .Include(x => x.ProductCategories).ThenInclude(c => c.Category)
            .Include(x => x.ProductAttributeValues).ThenInclude(a => a.Attribute)
            .Include(x => x.ProductLinkProducts).ThenInclude(p => p.LinkedProduct).ThenInclude(lp => lp.ThumbnailImage)
            .Include(x => x.Brand)
            .Include(x => x.ThumbnailImage)
            .Include(x => x.ProductMedia).ThenInclude(m => m.Media)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var lang = _culture.Language;

        var model = new ProductDetailModel
        {
            Id = product.Id,
            Name = product.Name.Resolve(lang)!,
            Brand = product.Brand is null
                ? null
                : new BrandInfo
                {
                    Id = product.Brand.Id,
                    Name = product.Brand.Name.Resolve(lang)!,
                    Slug = product.Brand.Slug
                },
            CalculatedProductPrice = _pricing.CalculateProductPrice(product),
            IsCallForPricing = product.IsCallForPricing,
            IsAllowToOrder = product.IsAllowToOrder,
            StockTrackingIsEnabled = product.StockTrackingIsEnabled,
            StockQuantity = product.StockQuantity,
            ShortDescription = product.ShortDescription?.Resolve(lang),
            Description = product.Description?.Resolve(lang),
            Specification = product.Specification,
            MetaTitle = product.MetaTitle?.Resolve(lang),
            MetaKeywords = product.MetaKeywords?.Resolve(lang),
            MetaDescription = product.MetaDescription?.Resolve(lang),
            ReviewsCount = product.ReviewsCount,
            RatingAverage = product.RatingAverage,
            ThumbnailImageUrl = _mediaUrl.GetUrl(product.ThumbnailImage?.FileName),
            ImageUrls = product.ProductMedia
                .OrderBy(m => m.DisplayOrder)
                .Select(m => _mediaUrl.GetUrl(m.Media.FileName))
                .Where(url => url != null)
                .Select(url => url!)
                .ToList(),
            Attributes = product.ProductAttributeValues
                .Select(x => new ProductDetailAttribute
                {
                    Name = x.Attribute.Name.Resolve(lang)!,
                    Value = x.Value,
                })
                .ToList(),
            Categories = product.ProductCategories
                .Select(x => new ProductDetailCategory
                {
                    Id = x.CategoryId,
                    Name = x.Category.Name.Resolve(lang)!,
                    Slug = x.Category.Slug
                })
                .ToList()
        };

        await MapVariationsAsync(product, model, cancellationToken);
        MapRelatedProducts(product, model);

        return model;
    }

    private async Task MapVariationsAsync(
        Product product, ProductDetailModel model, CancellationToken cancellationToken)
    {
        // A configurable parent owns ProductLink rows of type Super pointing at its variant children.
        if (!product.ProductLinkProducts.Any(x => x.LinkType == ProductLinkType.Super))
        {
            return;
        }

        var variations = await _db.Products
            .Include(x => x.ProductOptionCombinations).ThenInclude(o => o.Option)
            .Include(x => x.ThumbnailImage)
            .Include(x => x.ProductMedia).ThenInclude(m => m.Media)
            .Where(x => x.ProductLinkLinkedProducts.Any(
                link => link.ProductId == product.Id && link.LinkType == ProductLinkType.Super))
            .Where(x => x.IsPublished)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var lang = _culture.Language;

        foreach (var variation in variations)
        {
            var variationVm = new ProductDetailVariation
            {
                Id = variation.Id,
                Name = variation.Name.Resolve(lang)!,
                NormalizedName = variation.NormalizedName,
                IsAllowToOrder = variation.IsAllowToOrder,
                IsCallForPricing = variation.IsCallForPricing,
                StockTrackingIsEnabled = variation.StockTrackingIsEnabled,
                StockQuantity = variation.StockQuantity,
                ThumbnailImageUrl = _mediaUrl.GetUrl(variation.ThumbnailImage?.FileName),
                ImageUrls = variation.ProductMedia
                    .OrderBy(m => m.DisplayOrder)
                    .Select(m => _mediaUrl.GetUrl(m.Media.FileName))
                    .Where(url => url != null)
                    .Select(url => url!)
                    .ToList(),
                CalculatedProductPrice = _pricing.CalculateProductPrice(variation)
            };

            foreach (var combination in variation.ProductOptionCombinations.OrderBy(x => x.SortIndex))
            {
                variationVm.Options.Add(new ProductDetailVariationOption
                {
                    OptionId = combination.OptionId,
                    OptionName = combination.Option.Name.Resolve(lang)!,
                    Value = combination.Value
                });
            }

            model.Variations.Add(variationVm);
        }
    }

    private ProductListItem ToListItem(Product product)
    {
        var item = ProductListItem.FromProduct(product, _culture.Language);
        item.ThumbnailImageUrl = _mediaUrl.GetUrl(product.ThumbnailImage?.FileName);
        return item;
    }

    private void MapRelatedProducts(Product product, ProductDetailModel model)
    {
        var publishedLinks = product.ProductLinkProducts.Where(x =>
            x.LinkedProduct.IsPublished &&
            (x.LinkType == ProductLinkType.Related || x.LinkType == ProductLinkType.CrossSell));

        foreach (var link in publishedLinks)
        {
            var item = ToListItem(link.LinkedProduct);
            item.CalculatedProductPrice = _pricing.CalculateProductPrice(link.LinkedProduct);

            if (link.LinkType == ProductLinkType.Related)
            {
                model.RelatedProducts.Add(item);
            }

            if (link.LinkType == ProductLinkType.CrossSell)
            {
                model.CrossSellProducts.Add(item);
            }
        }
    }
}
