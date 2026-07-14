using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Product : ISeoEntity, ISoftDeletable, IAuditedEntity
{
    public long Id { get; set; }

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public string? Specification { get; set; }

    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }

    public decimal? SpecialPrice { get; set; }

    public DateTimeOffset? SpecialPriceStart { get; set; }

    public DateTimeOffset? SpecialPriceEnd { get; set; }

    public bool HasOptions { get; set; }

    public bool IsVisibleIndividually { get; set; }

    public bool IsFeatured { get; set; }

    /// <summary>Curated "Signature" flag — boosts the product first in default listings and the home rail.</summary>
    public bool IsSignature { get; set; }

    /// <summary>Lower sorts earlier among signature products; only meaningful when <see cref="IsSignature"/>.</summary>
    public int SignatureSortOrder { get; set; }

    public bool IsCallForPricing { get; set; }

    public bool IsAllowToOrder { get; set; }

    public bool StockTrackingIsEnabled { get; set; }

    public int StockQuantity { get; set; }

    public string? Sku { get; set; }

    public string? Gtin { get; set; }

    public string? NormalizedName { get; set; }

    public int DisplayOrder { get; set; }

    public long? VendorId { get; set; }

    public long? ThumbnailImageId { get; set; }

    public int ReviewsCount { get; set; }

    public double? RatingAverage { get; set; }

    public long? BrandId { get; set; }

    public long? TaxClassId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? MetaTitle { get; set; }

    public string? MetaKeywords { get; set; }

    public string? MetaDescription { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedOn { get; set; }

    public bool IsDeleted { get; set; }

    public long CreatedById { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public long LatestUpdatedById { get; set; }

    public Brand? Brand { get; set; }

    public ICollection<ProductAttributeValue> ProductAttributeValues { get; set; } = [];

    public ICollection<ProductCategory> ProductCategories { get; set; } = [];

    public ICollection<ProductLink> ProductLinkLinkedProducts { get; set; } = [];

    public ICollection<ProductLink> ProductLinkProducts { get; set; } = [];

    public ICollection<ProductMedium> ProductMedia { get; set; } = [];

    public ICollection<ProductOptionCombination> ProductOptionCombinations { get; set; } = [];

    public ICollection<ProductOptionValue> ProductOptionValues { get; set; } = [];

    public ICollection<ProductPriceHistory> ProductPriceHistories { get; set; } = [];

    public ICollection<CheckoutItem> CheckoutItems { get; set; } = [];

    public User CreatedBy { get; set; } = null!;

    public ICollection<StockHistory> StockHistories { get; set; } = [];

    public ICollection<Stock> Stocks { get; set; } = [];

    public User LatestUpdatedBy { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = [];

    public ICollection<ComparingProduct> ComparingProducts { get; set; } = [];

    public ICollection<ShipmentItem> ShipmentItems { get; set; } = [];

    public ICollection<CartItem> CartItems { get; set; } = [];

    public TaxClass? TaxClass { get; set; }

    public Medium? ThumbnailImage { get; set; }

    public ICollection<WishListItem> WishListItems { get; set; } = [];

    public ICollection<CartRule> CartRules { get; set; } = [];
}

