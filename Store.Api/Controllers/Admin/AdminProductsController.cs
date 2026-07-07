using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Catalog;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin product management, ported from SimplCommerce's <c>ProductApiController</c>:
/// scalars + SEO, categories, media (by uploaded media id), attribute values, options,
/// variations (child products linked via <c>ProductLinkType.Super</c> with option combinations,
/// matched by name) and related/cross-sell links. Deletes are soft (sets <c>IsDeleted</c>).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Catalog)]
[Route("api/admin/products")]
public sealed class AdminProductsController : ControllerBase
{
    /// <summary>PascalCase to stay byte-compatible with rows written by the old Newtonsoft-based admin.</summary>
    private static readonly JsonSerializerOptions OptionValueJson = new() { PropertyNamingPolicy = null };

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaStorage _mediaStorage;

    public AdminProductsController(StoreDbContext db, TimeProvider timeProvider, IMediaStorage mediaStorage)
    {
        _db = db;
        _timeProvider = timeProvider;
        _mediaStorage = mediaStorage;
    }

    /// <summary>Lists products (paged), optionally filtered by name, brand, category and publish state.
    /// Variation children (<c>IsVisibleIndividually == false</c>) are hidden unless
    /// <paramref name="includeVariations"/> is set. <paramref name="deletedOnly"/> narrows the list to
    /// soft-deleted products (it implies <paramref name="includeDeleted"/>).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductListItem>>> List(
        [FromQuery] string? query, [FromQuery] bool includeDeleted = false, [FromQuery] bool includeVariations = false,
        [FromQuery] bool deletedOnly = false, [FromQuery] bool? isPublished = null,
        [FromQuery] long? brandId = null, [FromQuery] long? categoryId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var products = _db.Products.AsQueryable();
        if (deletedOnly)
        {
            products = products.Where(p => p.IsDeleted);
        }
        else if (!includeDeleted)
        {
            products = products.Where(p => !p.IsDeleted);
        }

        if (!includeVariations)
        {
            products = products.Where(p => p.IsVisibleIndividually);
        }

        if (isPublished.HasValue)
        {
            products = products.Where(p => p.IsPublished == isPublished.Value);
        }

        if (brandId.HasValue)
        {
            products = products.Where(p => p.BrandId == brandId.Value);
        }

        if (categoryId.HasValue)
        {
            products = products.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            products = products.Where(p => p.Name.Contains(query));
        }

        var items = await products
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new AdminProductListItem(
                p.Id, p.Name, p.Slug, p.Price, p.OldPrice, p.StockQuantity, p.IsPublished, p.IsDeleted, p.BrandId,
                p.HasOptions, p.IsVisibleIndividually, p.ThumbnailImage != null ? p.ThumbnailImage.FileName : null))
            .ToListAsync(cancellationToken);

        return Ok(items.Select(i => i with { ThumbnailUrl = _mediaStorage.GetUrl(i.ThumbnailUrl) }).ToList());
    }

    /// <summary>Name search for the related/cross-sell product pickers (simple products only).</summary>
    [HttpGet("quick-search")]
    public async Task<ActionResult<IReadOnlyList<ProductQuickSearchItem>>> QuickSearch(
        [FromQuery] string? query, CancellationToken cancellationToken)
    {
        var products = _db.Products.Where(p => !p.IsDeleted && !p.HasOptions && p.IsVisibleIndividually);
        if (!string.IsNullOrWhiteSpace(query))
        {
            products = products.Where(p => p.Name.Contains(query));
        }

        var items = await products
            .OrderBy(p => p.Name)
            .Take(8)
            .Select(p => new ProductQuickSearchItem(p.Id, p.Name, p.Sku, p.IsPublished))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminProductDetail>> Get(long id, CancellationToken cancellationToken)
    {
        var product = await LoadAggregateAsync(id, cancellationToken);
        return product == null ? NotFound() : Ok(ToDetail(product));
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductDetail>> Create(
        ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();

        var product = new Product
        {
            CreatedById = userId,
            CreatedOn = now,
            LatestUpdatedById = userId,
            LatestUpdatedOn = now
        };

        Apply(product, request);

        if (await _db.Products.AnyAsync(p => p.Slug == product.Slug, cancellationToken))
        {
            return Conflict(new { error = $"A product with slug '{product.Slug}' already exists." });
        }

        _db.Products.Add(product);
        product.ProductPriceHistories.Add(CreatePriceHistory(userId, now, product));
        await _db.SaveChangesAsync(cancellationToken);

        await ReconcileChildCollectionsAsync(product, request, userId, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await LoadAggregateAsync(product.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, ToDetail(created!));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminProductDetail>> Update(
        long id, ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var product = await LoadAggregateAsync(id, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();

        var isPriceChanged = product.Price != request.Price ||
            product.OldPrice != request.OldPrice ||
            product.SpecialPrice != request.SpecialPrice ||
            product.SpecialPriceStart != request.SpecialPriceStart ||
            product.SpecialPriceEnd != request.SpecialPriceEnd;

        Apply(product, request);
        product.LatestUpdatedById = userId;
        product.LatestUpdatedOn = now;

        if (await _db.Products.AnyAsync(p => p.Slug == product.Slug && p.Id != id, cancellationToken))
        {
            return Conflict(new { error = $"A product with slug '{product.Slug}' already exists." });
        }

        if (isPriceChanged)
        {
            product.ProductPriceHistories.Add(CreatePriceHistory(userId, now, product));
        }

        await ReconcileChildCollectionsAsync(product, request, userId, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var updated = await LoadAggregateAsync(id, cancellationToken);
        return Ok(ToDetail(updated!));
    }

    /// <summary>Soft-deletes the product and its variation children.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(p => p.ProductLinkProducts).ThenInclude(l => l.LinkedProduct)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();

        product.IsDeleted = true;
        product.LatestUpdatedById = userId;
        product.LatestUpdatedOn = now;

        foreach (var link in product.ProductLinkProducts.Where(l => l.LinkType == ProductLinkType.Super))
        {
            link.LinkedProduct.IsDeleted = true;
            link.LinkedProduct.LatestUpdatedById = userId;
            link.LinkedProduct.LatestUpdatedOn = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ----- Save pipeline ----------------------------------------------------------------------

    private Task<Product?> LoadAggregateAsync(long id, CancellationToken cancellationToken) =>
        _db.Products
            .Include(p => p.ThumbnailImage)
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductMedia).ThenInclude(m => m.Media)
            .Include(p => p.ProductAttributeValues).ThenInclude(a => a.Attribute).ThenInclude(a => a.Group)
            .Include(p => p.ProductOptionValues).ThenInclude(o => o.Option)
            .Include(p => p.ProductLinkProducts).ThenInclude(l => l.LinkedProduct).ThenInclude(lp => lp.ThumbnailImage)
            .Include(p => p.ProductLinkProducts).ThenInclude(l => l.LinkedProduct).ThenInclude(lp => lp.ProductOptionCombinations).ThenInclude(c => c.Option)
            .Include(p => p.ProductLinkProducts).ThenInclude(l => l.LinkedProduct).ThenInclude(lp => lp.ProductMedia).ThenInclude(m => m.Media)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    private void Apply(Product product, ProductUpsertRequest request)
    {
        product.Name = request.Name;
        product.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        product.NormalizedName = request.Name.ToUpperInvariant();
        product.ShortDescription = request.ShortDescription;
        product.Description = request.Description;
        product.Specification = request.Specification;
        product.MetaTitle = request.MetaTitle;
        product.MetaKeywords = request.MetaKeywords;
        product.MetaDescription = request.MetaDescription;
        product.Price = request.Price;
        product.OldPrice = request.OldPrice;
        product.SpecialPrice = request.SpecialPrice;
        product.SpecialPriceStart = request.SpecialPriceStart;
        product.SpecialPriceEnd = request.SpecialPriceEnd;
        product.Sku = request.Sku;
        product.Gtin = request.Gtin;
        product.IsPublished = request.IsPublished;
        product.PublishedOn ??= request.IsPublished ? _timeProvider.GetUtcNow() : null;
        product.IsFeatured = request.IsFeatured;
        product.IsAllowToOrder = request.IsAllowToOrder;
        product.IsCallForPricing = request.IsCallForPricing;
        product.IsVisibleIndividually = true;
        product.HasOptions = request.Variations.Count > 0;
        product.StockTrackingIsEnabled = request.StockTrackingIsEnabled;
        product.StockQuantity = request.StockQuantity;
        product.DisplayOrder = request.DisplayOrder;
        product.BrandId = request.BrandId;
        product.TaxClassId = request.TaxClassId;
        product.ThumbnailImageId = request.ThumbnailImageId;
    }

    private async Task ReconcileChildCollectionsAsync(
        Product product, ProductUpsertRequest request, long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await ReconcileCategoriesAsync(product, request.CategoryIds, cancellationToken);
        await ReconcileMediaAsync(product, request.MediaIds, cancellationToken);
        await ReconcileOptionsAsync(product, request.Options, cancellationToken);
        await ReconcileAttributesAsync(product, request.Attributes, cancellationToken);
        await ReconcileVariationsAsync(product, request.Variations, userId, now, cancellationToken);
        await ReconcileLinksAsync(product, ProductLinkType.Related, request.RelatedProductIds, cancellationToken);
        await ReconcileLinksAsync(product, ProductLinkType.CrossSell, request.CrossSellProductIds, cancellationToken);
    }

    private async Task ReconcileCategoriesAsync(Product product, IList<long> categoryIds, CancellationToken cancellationToken)
    {
        var existing = await _db.ProductCategories.Where(pc => pc.ProductId == product.Id).ToListAsync(cancellationToken);
        _db.ProductCategories.RemoveRange(existing.Where(pc => !categoryIds.Contains(pc.CategoryId)));

        foreach (var categoryId in categoryIds.Distinct().Where(id => existing.All(pc => pc.CategoryId != id)))
        {
            _db.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = categoryId });
        }
    }

    private async Task ReconcileMediaAsync(Product product, IList<long> mediaIds, CancellationToken cancellationToken)
    {
        var existing = await _db.ProductMedia.Where(pm => pm.ProductId == product.Id).ToListAsync(cancellationToken);
        _db.ProductMedia.RemoveRange(existing.Where(pm => !mediaIds.Contains(pm.MediaId)));

        var order = 0;
        foreach (var mediaId in mediaIds)
        {
            var row = existing.FirstOrDefault(pm => pm.MediaId == mediaId);
            if (row == null)
            {
                _db.ProductMedia.Add(new ProductMedium { ProductId = product.Id, MediaId = mediaId, DisplayOrder = order });
            }
            else
            {
                row.DisplayOrder = order;
            }

            order++;
        }
    }

    private async Task ReconcileOptionsAsync(Product product, IList<ProductOptionRequest> options, CancellationToken cancellationToken)
    {
        var existing = await _db.ProductOptionValues.Where(ov => ov.ProductId == product.Id).ToListAsync(cancellationToken);
        _db.ProductOptionValues.RemoveRange(existing.Where(ov => options.All(o => o.OptionId != ov.OptionId)));

        var sortIndex = 0;
        foreach (var option in options)
        {
            var value = JsonSerializer.Serialize(option.Values, OptionValueJson);
            var row = existing.FirstOrDefault(ov => ov.OptionId == option.OptionId);
            if (row == null)
            {
                _db.ProductOptionValues.Add(new ProductOptionValue
                {
                    ProductId = product.Id,
                    OptionId = option.OptionId,
                    DisplayType = option.DisplayType,
                    Value = value,
                    SortIndex = sortIndex
                });
            }
            else
            {
                row.Value = value;
                row.DisplayType = option.DisplayType;
                row.SortIndex = sortIndex;
            }

            sortIndex++;
        }
    }

    private async Task ReconcileAttributesAsync(
        Product product, IList<ProductAttributeValueRequest> attributes, CancellationToken cancellationToken)
    {
        var existing = await _db.ProductAttributeValues.Where(av => av.ProductId == product.Id).ToListAsync(cancellationToken);
        _db.ProductAttributeValues.RemoveRange(existing.Where(av => attributes.All(a => a.AttributeId != av.AttributeId)));

        foreach (var attribute in attributes)
        {
            var row = existing.FirstOrDefault(av => av.AttributeId == attribute.AttributeId);
            if (row == null)
            {
                _db.ProductAttributeValues.Add(new ProductAttributeValue
                {
                    ProductId = product.Id,
                    AttributeId = attribute.AttributeId,
                    Value = attribute.Value
                });
            }
            else
            {
                row.Value = attribute.Value;
            }
        }
    }

    /// <summary>Variations are matched by name, like the old admin: new names create a cloned child
    /// product linked with <c>Super</c>; missing names soft-delete the child and remove the link.</summary>
    private async Task ReconcileVariationsAsync(
        Product product, IList<ProductVariationRequest> variations, long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var links = await _db.ProductLinks
            .Include(l => l.LinkedProduct).ThenInclude(lp => lp.ProductOptionCombinations)
            .Where(l => l.ProductId == product.Id && l.LinkType == ProductLinkType.Super)
            .ToListAsync(cancellationToken);

        foreach (var variation in variations)
        {
            var link = links.FirstOrDefault(l => l.LinkedProduct.Name == variation.Name);
            if (link == null)
            {
                var child = CloneForVariation(product);
                child.Name = variation.Name;
                child.Slug = Slug.Generate(variation.Name);
                child.NormalizedName = variation.Name.ToUpperInvariant();
                child.Sku = variation.Sku;
                child.Gtin = variation.Gtin;
                child.Price = variation.Price;
                child.OldPrice = variation.OldPrice;
                child.ThumbnailImageId = variation.ThumbnailImageId ?? product.ThumbnailImageId;
                child.CreatedById = userId;
                child.CreatedOn = now;
                child.LatestUpdatedById = userId;
                child.LatestUpdatedOn = now;

                foreach (var combination in variation.OptionCombinations)
                {
                    child.ProductOptionCombinations.Add(new ProductOptionCombination
                    {
                        OptionId = combination.OptionId,
                        Value = combination.Value,
                        SortIndex = combination.SortIndex
                    });
                }

                foreach (var (mediaId, index) in variation.MediaIds.Select((m, i) => (m, i)))
                {
                    child.ProductMedia.Add(new ProductMedium { MediaId = mediaId, DisplayOrder = index });
                }

                child.ProductPriceHistories.Add(CreatePriceHistory(userId, now, child, variation.Price, variation.OldPrice));

                _db.ProductLinks.Add(new ProductLink
                {
                    ProductId = product.Id,
                    LinkedProduct = child,
                    LinkType = ProductLinkType.Super
                });
            }
            else
            {
                var child = link.LinkedProduct;
                var isPriceChanged = child.Price != variation.Price || child.OldPrice != variation.OldPrice;

                child.Sku = variation.Sku;
                child.Gtin = variation.Gtin;
                child.Price = variation.Price;
                child.OldPrice = variation.OldPrice;
                child.IsDeleted = false;
                child.StockTrackingIsEnabled = product.StockTrackingIsEnabled;
                child.LatestUpdatedById = userId;
                child.LatestUpdatedOn = now;
                if (variation.ThumbnailImageId.HasValue)
                {
                    child.ThumbnailImageId = variation.ThumbnailImageId;
                }

                if (isPriceChanged)
                {
                    child.ProductPriceHistories.Add(CreatePriceHistory(userId, now, child, variation.Price, variation.OldPrice));
                }
            }
        }

        foreach (var link in links.Where(l => variations.All(v => v.Name != l.LinkedProduct.Name)))
        {
            link.LinkedProduct.IsDeleted = true;
            link.LinkedProduct.LatestUpdatedById = userId;
            link.LinkedProduct.LatestUpdatedOn = now;
            _db.ProductLinks.Remove(link);
        }
    }

    private async Task ReconcileLinksAsync(
        Product product, int linkType, IList<long> linkedProductIds, CancellationToken cancellationToken)
    {
        var links = await _db.ProductLinks
            .Where(l => l.ProductId == product.Id && l.LinkType == linkType)
            .ToListAsync(cancellationToken);

        _db.ProductLinks.RemoveRange(links.Where(l => !linkedProductIds.Contains(l.LinkedProductId)));

        foreach (var linkedId in linkedProductIds.Distinct().Where(id => links.All(l => l.LinkedProductId != id)))
        {
            _db.ProductLinks.Add(new ProductLink { ProductId = product.Id, LinkedProductId = linkedId, LinkType = linkType });
        }
    }

    /// <summary>Port of the old <c>Product.Clone()</c> used when spawning a variation child.</summary>
    private static Product CloneForVariation(Product parent) => new()
    {
        Name = parent.Name,
        Slug = parent.Slug,
        MetaTitle = parent.MetaTitle,
        MetaKeywords = parent.MetaKeywords,
        MetaDescription = parent.MetaDescription,
        ShortDescription = parent.ShortDescription,
        Description = parent.Description,
        Specification = parent.Specification,
        IsPublished = parent.IsPublished,
        PublishedOn = parent.PublishedOn,
        Price = parent.Price,
        OldPrice = parent.OldPrice,
        SpecialPrice = parent.SpecialPrice,
        SpecialPriceStart = parent.SpecialPriceStart,
        SpecialPriceEnd = parent.SpecialPriceEnd,
        HasOptions = false,
        IsVisibleIndividually = false,
        IsFeatured = parent.IsFeatured,
        IsAllowToOrder = parent.IsAllowToOrder,
        IsCallForPricing = parent.IsCallForPricing,
        StockQuantity = parent.StockQuantity,
        StockTrackingIsEnabled = parent.StockTrackingIsEnabled,
        BrandId = parent.BrandId,
        VendorId = parent.VendorId,
        TaxClassId = parent.TaxClassId,
        DisplayOrder = parent.DisplayOrder
    };

    private static ProductPriceHistory CreatePriceHistory(
        long userId, DateTimeOffset now, Product product, decimal? price = null, decimal? oldPrice = null) => new()
    {
        CreatedById = userId,
        CreatedOn = now,
        Price = price ?? product.Price,
        OldPrice = oldPrice ?? product.OldPrice,
        SpecialPrice = product.SpecialPrice,
        SpecialPriceStart = product.SpecialPriceStart,
        SpecialPriceEnd = product.SpecialPriceEnd
    };

    // ----- Mapping ------------------------------------------------------------------------------

    private AdminProductDetail ToDetail(Product p)
    {
        var media = p.ProductMedia
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new AdminProductMediaDto(m.MediaId, _mediaStorage.GetUrl(m.Media.FileName)!, m.Media.Caption, m.Media.MediaType))
            .ToList();

        var options = p.ProductOptionValues
            .OrderBy(o => o.SortIndex)
            .Select(o => new AdminProductOptionDto(
                o.OptionId, o.Option.Name, o.DisplayType, DeserializeOptionValues(o.Value)))
            .ToList();

        var attributes = p.ProductAttributeValues
            .Select(a => new AdminProductAttributeValueDto(a.AttributeId, a.Attribute.Name, a.Attribute.Group?.Name, a.Value))
            .ToList();

        var variations = p.ProductLinkProducts
            .Where(l => l.LinkType == ProductLinkType.Super && !l.LinkedProduct.IsDeleted)
            .Select(l => l.LinkedProduct)
            .OrderBy(v => v.Id)
            .Select(v => new AdminProductVariationDto(
                v.Id, v.Name, v.Sku, v.Gtin, v.Price, v.OldPrice,
                v.ThumbnailImageId, _mediaStorage.GetUrl(v.ThumbnailImage?.FileName),
                v.ProductMedia.OrderBy(m => m.DisplayOrder)
                    .Select(m => new AdminProductMediaDto(m.MediaId, _mediaStorage.GetUrl(m.Media.FileName)!, m.Media.Caption, m.Media.MediaType))
                    .ToList(),
                v.ProductOptionCombinations.OrderBy(c => c.SortIndex)
                    .Select(c => new AdminProductOptionCombinationDto(c.OptionId, c.Option.Name, c.Value, c.SortIndex))
                    .ToList()))
            .ToList();

        var related = LinkedProducts(p, ProductLinkType.Related);
        var crossSell = LinkedProducts(p, ProductLinkType.CrossSell);

        return new AdminProductDetail(
            p.Id, p.Name, p.Slug, p.ShortDescription, p.Description, p.Specification,
            p.MetaTitle, p.MetaKeywords, p.MetaDescription,
            p.Price, p.OldPrice, p.SpecialPrice, p.SpecialPriceStart, p.SpecialPriceEnd,
            p.Sku, p.Gtin, p.IsPublished, p.IsFeatured, p.IsAllowToOrder, p.IsCallForPricing,
            p.StockTrackingIsEnabled, p.StockQuantity, p.DisplayOrder, p.BrandId, p.TaxClassId,
            p.IsDeleted, p.ProductCategories.Select(pc => pc.CategoryId).ToList(),
            p.ThumbnailImageId, _mediaStorage.GetUrl(p.ThumbnailImage?.FileName),
            media, attributes, options, variations, related, crossSell);
    }

    private static List<AdminProductLinkDto> LinkedProducts(Product p, int linkType) =>
        p.ProductLinkProducts
            .Where(l => l.LinkType == linkType && !l.LinkedProduct.IsDeleted)
            .Select(l => l.LinkedProduct)
            .OrderBy(lp => lp.Id)
            .Select(lp => new AdminProductLinkDto(lp.Id, lp.Name, lp.IsPublished))
            .ToList();

    private static IReadOnlyList<ProductOptionValueItem> DeserializeOptionValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProductOptionValueItem>>(json, OptionValueJson) ?? [];
        }
        catch (JsonException)
        {
            // Old rows may hold a plain string array (very early SimplCommerce format).
            try
            {
                var plain = JsonSerializer.Deserialize<List<string>>(json);
                return plain?.Select(v => new ProductOptionValueItem { Key = v }).ToList() ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
