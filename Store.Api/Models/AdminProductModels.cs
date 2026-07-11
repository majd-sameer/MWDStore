using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

public sealed class ProductUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional; generated from <see cref="Name"/> when omitted.</summary>
    public string? Slug { get; set; }

    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? Specification { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }

    // ----- English translation (LocalizedString.En) -----------------------------------------------
    // A null/empty value clears an existing translation (LocalizedString.From normalizes empty to
    // null); a non-empty value sets it.
    public string? NameEn { get; set; }
    public string? ShortDescriptionEn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? MetaTitleEn { get; set; }
    public string? MetaKeywordsEn { get; set; }
    public string? MetaDescriptionEn { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }
    public decimal? SpecialPrice { get; set; }
    public DateTimeOffset? SpecialPriceStart { get; set; }
    public DateTimeOffset? SpecialPriceEnd { get; set; }

    public string? Sku { get; set; }
    public string? Gtin { get; set; }

    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsAllowToOrder { get; set; } = true;
    public bool IsCallForPricing { get; set; }
    public bool StockTrackingIsEnabled { get; set; }
    public int StockQuantity { get; set; }
    public int DisplayOrder { get; set; }

    public long? BrandId { get; set; }
    public long? TaxClassId { get; set; }

    /// <summary>Category ids the product belongs to (replaces the existing set on update).</summary>
    public IList<long> CategoryIds { get; set; } = new List<long>();

    /// <summary>Id of an uploaded <c>Medium</c> used as the thumbnail; null clears it.</summary>
    public long? ThumbnailImageId { get; set; }

    /// <summary>Gallery media ids in display order (replaces the existing set on update).</summary>
    public IList<long> MediaIds { get; set; } = new List<long>();

    public IList<ProductAttributeValueRequest> Attributes { get; set; } = new List<ProductAttributeValueRequest>();
    public IList<ProductOptionRequest> Options { get; set; } = new List<ProductOptionRequest>();
    public IList<ProductVariationRequest> Variations { get; set; } = new List<ProductVariationRequest>();
    public IList<long> RelatedProductIds { get; set; } = new List<long>();
    public IList<long> CrossSellProductIds { get; set; } = new List<long>();
}

public sealed record AdminProductDetail(
    long Id, string Name, string Slug, string? ShortDescription, string? Description, string? Specification,
    string? MetaTitle, string? MetaKeywords, string? MetaDescription,
    decimal Price, decimal? OldPrice, decimal? SpecialPrice, DateTimeOffset? SpecialPriceStart, DateTimeOffset? SpecialPriceEnd,
    string? Sku, string? Gtin, bool IsPublished, bool IsFeatured, bool IsAllowToOrder, bool IsCallForPricing,
    bool StockTrackingIsEnabled, int StockQuantity, int DisplayOrder, long? BrandId, long? TaxClassId,
    bool IsDeleted, IReadOnlyList<long> CategoryIds,
    long? ThumbnailImageId, string? ThumbnailUrl,
    IReadOnlyList<AdminProductMediaDto> Media,
    IReadOnlyList<AdminProductAttributeValueDto> Attributes,
    IReadOnlyList<AdminProductOptionDto> Options,
    IReadOnlyList<AdminProductVariationDto> Variations,
    IReadOnlyList<AdminProductLinkDto> RelatedProducts,
    IReadOnlyList<AdminProductLinkDto> CrossSellProducts,
    string? NameEn, string? ShortDescriptionEn, string? DescriptionEn,
    string? MetaTitleEn, string? MetaKeywordsEn, string? MetaDescriptionEn);

/// <summary>List item shape extended with the EN-availability flag. Kept here (not alongside the
/// shared <c>AdminProductListItem</c> record in <c>AdminModels.cs</c>, which is owned by a different
/// feature lane's file set) so this feature's changes stay inside its own owned files.</summary>
/// <param name="HasEnglish">True when an English <c>Name</c> overlay row exists for this product.</param>
public sealed record AdminProductListItemDto(
    long Id, string Name, string Slug, decimal Price, decimal? OldPrice,
    int StockQuantity, bool IsPublished, bool IsDeleted, long? BrandId,
    bool HasOptions, bool IsVisibleIndividually, string? ThumbnailUrl,
    bool HasEnglish);
