using Store.Application.Catalog.Pricing;
using Store.Domain;

namespace Store.Application.Catalog.Models;

/// <summary>
/// Port of SimplCommerce's <c>ProductThumbnail</c> (storefront list item). Carries the raw price
/// fields plus the resolved <see cref="CalculatedProductPrice"/>.
/// </summary>
public sealed class ProductListItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? SpecialPrice { get; set; }
    public DateTimeOffset? SpecialPriceStart { get; set; }
    public DateTimeOffset? SpecialPriceEnd { get; set; }
    public bool IsCallForPricing { get; set; }
    public bool IsAllowToOrder { get; set; }
    public int? StockQuantity { get; set; }
    public int ReviewsCount { get; set; }
    public double? RatingAverage { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    public string? ShortDescription { get; set; }

    /// <summary>First category name — the storefront card eyebrow / list-row label. Requires the
    /// source query to include <c>ProductCategories.Category</c>; null otherwise.</summary>
    public string? CategoryName { get; set; }

    /// <summary>First category slug — lets the storefront translate the category label by slug
    /// (falling back to <see cref="CategoryName"/>). Same source include as the name.</summary>
    public string? CategorySlug { get; set; }

    public CalculatedProductPrice? CalculatedProductPrice { get; set; }

    public static ProductListItem FromProduct(Product product, ContentLanguage lang) => new()
    {
        Id = product.Id,
        Name = product.Name.Resolve(lang)!,
        Slug = product.Slug,
        Price = product.Price,
        OldPrice = product.OldPrice,
        SpecialPrice = product.SpecialPrice,
        SpecialPriceStart = product.SpecialPriceStart,
        SpecialPriceEnd = product.SpecialPriceEnd,
        StockQuantity = product.StockQuantity,
        IsAllowToOrder = product.IsAllowToOrder,
        IsCallForPricing = product.IsCallForPricing,
        ReviewsCount = product.ReviewsCount,
        RatingAverage = product.RatingAverage,
        ShortDescription = product.ShortDescription?.Resolve(lang),
        CategoryName = product.ProductCategories?.FirstOrDefault()?.Category?.Name.Resolve(lang),
        CategorySlug = product.ProductCategories?.FirstOrDefault()?.Category?.Slug
    };
}
