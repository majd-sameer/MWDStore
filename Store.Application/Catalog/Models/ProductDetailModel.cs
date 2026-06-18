using Store.Application.Catalog.Pricing;

namespace Store.Application.Catalog.Models;

/// <summary>
/// Port of SimplCommerce's <c>ProductDetail</c> view model (the parts that are pure domain logic;
/// media, localization and currency formatting are out of scope here).
/// </summary>
public sealed class ProductDetailModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BrandInfo? Brand { get; set; }
    public CalculatedProductPrice? CalculatedProductPrice { get; set; }
    public bool IsCallForPricing { get; set; }
    public bool IsAllowToOrder { get; set; }
    public bool StockTrackingIsEnabled { get; set; }
    public int StockQuantity { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public int ReviewsCount { get; set; }
    public double? RatingAverage { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    public IList<string> ImageUrls { get; set; } = new List<string>();

    public IList<ProductDetailAttribute> Attributes { get; set; } = new List<ProductDetailAttribute>();
    public IList<ProductDetailCategory> Categories { get; set; } = new List<ProductDetailCategory>();
    public IList<ProductDetailVariation> Variations { get; set; } = new List<ProductDetailVariation>();
    public IList<ProductListItem> RelatedProducts { get; set; } = new List<ProductListItem>();
    public IList<ProductListItem> CrossSellProducts { get; set; } = new List<ProductListItem>();
}

public sealed class BrandInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class ProductDetailAttribute
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public sealed class ProductDetailCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class ProductDetailVariation
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NormalizedName { get; set; }
    public bool IsCallForPricing { get; set; }
    public bool IsAllowToOrder { get; set; }
    public int StockQuantity { get; set; }
    public bool StockTrackingIsEnabled { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    public IList<string> ImageUrls { get; set; } = new List<string>();
    public CalculatedProductPrice? CalculatedProductPrice { get; set; }
    public IList<ProductDetailVariationOption> Options { get; set; } = new List<ProductDetailVariationOption>();
}

public sealed class ProductDetailVariationOption
{
    public long OptionId { get; set; }
    public string OptionName { get; set; } = string.Empty;
    public string? Value { get; set; }
}
