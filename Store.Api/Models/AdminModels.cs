using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

// ----- Media --------------------------------------------------------------------------------------

public sealed record MediaDto(long Id, string? FileName, string Url, string? Caption, int MediaType);

// ----- Products -----------------------------------------------------------------------------------

public sealed record AdminProductListItem(
    long Id, string Name, string Slug, decimal Price, decimal? OldPrice,
    int StockQuantity, bool IsPublished, bool IsDeleted, long? BrandId,
    bool HasOptions, bool IsVisibleIndividually, bool IsSignature, string? ThumbnailUrl);

/// <summary>One value of a product option (mirrors SimplCommerce's <c>ProductOptionValueVm</c>;
/// the list is JSON-serialized into <c>ProductOptionValue.Value</c>).</summary>
public sealed class ProductOptionValueItem
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Presentation value, e.g. a hex color when DisplayType is "color".</summary>
    public string? Display { get; set; }
}

public sealed class ProductOptionRequest
{
    [Required]
    public long OptionId { get; set; }

    /// <summary>"text" or "color".</summary>
    public string? DisplayType { get; set; }

    public IList<ProductOptionValueItem> Values { get; set; } = new List<ProductOptionValueItem>();
}

public sealed class ProductOptionCombinationRequest
{
    [Required]
    public long OptionId { get; set; }

    [Required]
    public string Value { get; set; } = string.Empty;

    public int SortIndex { get; set; }
}

/// <summary>A variation (child product linked with <c>ProductLinkType.Super</c>), identified by Name.</summary>
public sealed class ProductVariationRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }
    public string? Gtin { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }
    public long? ThumbnailImageId { get; set; }
    public IList<long> MediaIds { get; set; } = new List<long>();
    public IList<ProductOptionCombinationRequest> OptionCombinations { get; set; } = new List<ProductOptionCombinationRequest>();
}

public sealed class ProductAttributeValueRequest
{
    [Required]
    public long AttributeId { get; set; }

    public string? Value { get; set; }
}

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
    public bool IsSignature { get; set; }
    public int SignatureSortOrder { get; set; }
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

public sealed record AdminProductMediaDto(long MediaId, string Url, string? Caption, int MediaType);

public sealed record AdminProductOptionDto(
    long OptionId, string Name, string? DisplayType, IReadOnlyList<ProductOptionValueItem> Values);

public sealed record AdminProductOptionCombinationDto(long OptionId, string OptionName, string? Value, int SortIndex);

public sealed record AdminProductVariationDto(
    long Id, string Name, string? Sku, string? Gtin, decimal Price, decimal? OldPrice,
    long? ThumbnailImageId, string? ThumbnailUrl, IReadOnlyList<AdminProductMediaDto> Media,
    IReadOnlyList<AdminProductOptionCombinationDto> OptionCombinations);

public sealed record AdminProductLinkDto(long Id, string Name, bool IsPublished);

public sealed record AdminProductAttributeValueDto(long AttributeId, string Name, string? GroupName, string? Value);

public sealed record AdminProductDetail(
    long Id, string Name, string Slug, string? ShortDescription, string? Description, string? Specification,
    string? MetaTitle, string? MetaKeywords, string? MetaDescription,
    decimal Price, decimal? OldPrice, decimal? SpecialPrice, DateTimeOffset? SpecialPriceStart, DateTimeOffset? SpecialPriceEnd,
    string? Sku, string? Gtin, bool IsPublished, bool IsFeatured, bool IsSignature, int SignatureSortOrder,
    bool IsAllowToOrder, bool IsCallForPricing,
    bool StockTrackingIsEnabled, int StockQuantity, int DisplayOrder, long? BrandId, long? TaxClassId,
    bool IsDeleted, IReadOnlyList<long> CategoryIds,
    long? ThumbnailImageId, string? ThumbnailUrl,
    IReadOnlyList<AdminProductMediaDto> Media,
    IReadOnlyList<AdminProductAttributeValueDto> Attributes,
    IReadOnlyList<AdminProductOptionDto> Options,
    IReadOnlyList<AdminProductVariationDto> Variations,
    IReadOnlyList<AdminProductLinkDto> RelatedProducts,
    IReadOnlyList<AdminProductLinkDto> CrossSellProducts);

/// <summary>Body for the list-page quick toggle (PATCH /api/admin/products/{id}/signature).</summary>
public sealed class ProductSignatureRequest
{
    public bool IsSignature { get; set; }

    public int? SignatureSortOrder { get; set; }
}

/// <summary>Lightweight result for the related/cross-sell product picker.</summary>
public sealed record ProductQuickSearchItem(long Id, string Name, string? Sku, bool IsPublished);

// ----- Product options (admin CRUD) ----------------------------------------------------------------

public sealed record AdminProductOptionListItem(long Id, string Name);

public sealed class ProductOptionUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

// ----- Product attributes (admin CRUD) --------------------------------------------------------------

public sealed record AdminProductAttributeDto(long Id, string Name, long GroupId, string GroupName);

public sealed class ProductAttributeUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public long GroupId { get; set; }
}

public sealed record AdminProductAttributeGroupDto(long Id, string Name);

public sealed class ProductAttributeGroupUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

// ----- Categories ---------------------------------------------------------------------------------

public sealed class CategoryUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IncludeInMenu { get; set; }
    public long? ParentId { get; set; }
}

public sealed record AdminCategoryDto(
    long Id, string Name, string Slug, string? Description, int DisplayOrder,
    bool IsPublished, bool IncludeInMenu, long? ParentId, bool IsDeleted);

// ----- Brands -------------------------------------------------------------------------------------

public sealed class BrandUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Description { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed record AdminBrandDto(
    long Id, string Name, string Slug, string? Description, bool IsPublished, bool IsDeleted);

// ----- Order management ---------------------------------------------------------------------------

public sealed class UpdateOrderStatusRequest
{
    [Required]
    public int OrderStatus { get; set; }
}

// ----- Inventory ----------------------------------------------------------------------------------

public sealed record StockRowDto(long WarehouseId, string WarehouseName, int Quantity, int ReservedQuantity);

public sealed record ProductStockDto(long ProductId, string ProductName, int ProductStockQuantity, IReadOnlyList<StockRowDto> Warehouses);

public sealed class StockAdjustmentRequest
{
    [Required]
    public long ProductId { get; set; }

    [Required]
    public long WarehouseId { get; set; }

    /// <summary>Positive to add, negative to remove (clamped so warehouse stock never goes below zero).</summary>
    public int AdjustedQuantity { get; set; }

    public string? Note { get; set; }
}

// ----- Tax ----------------------------------------------------------------------------------------

public sealed record AdminTaxClassDto(long Id, string Name);

public sealed class TaxClassUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

public sealed record AdminTaxRateDto(
    long Id, long TaxClassId, string TaxClassName, string? CountryId, string? CountryName,
    long? StateOrProvinceId, string? StateOrProvinceName, string? ZipCode, decimal Rate);

public sealed class TaxRateUpsertRequest
{
    [Required]
    public long TaxClassId { get; set; }

    public string? CountryId { get; set; }
    public long? StateOrProvinceId { get; set; }
    public string? ZipCode { get; set; }

    [Range(0, 100)]
    public decimal Rate { get; set; }
}

// ----- Shipping -------------------------------------------------------------------------------------

public sealed record AdminShippingProviderDto(
    string Id, string Name, bool IsEnabled, decimal? FreeShippingMinimumOrderAmount);

public sealed class ShippingProviderUpdateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>Only meaningful for the "Free" provider.</summary>
    public decimal? FreeShippingMinimumOrderAmount { get; set; }
}

public sealed record AdminTableRateDto(
    long Id, string? ShippingProviderId, string? ShippingProviderName,
    string? CountryId, string? CountryName, long? StateOrProvinceId, string? StateOrProvinceName,
    string? ZipCode, decimal MinOrderSubtotal, decimal ShippingPrice, string? Note);

public sealed class TableRateUpsertRequest
{
    /// <summary>The carrier this rate belongs to (e.g. "Aramex", "JordanPost"). Required.</summary>
    [Required]
    public string ShippingProviderId { get; set; } = string.Empty;

    public string? CountryId { get; set; }
    public long? StateOrProvinceId { get; set; }
    public string? ZipCode { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MinOrderSubtotal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ShippingPrice { get; set; }

    public string? Note { get; set; }
}

// ----- Warehouses ------------------------------------------------------------------------------------

public sealed record AdminWarehouseDto(
    long Id, string Name, string? ContactName, string? Phone, string? AddressLine1, string? AddressLine2,
    string? City, string? ZipCode, long StateOrProvinceId, string? StateOrProvinceName,
    string CountryId, string? CountryName);

public sealed class WarehouseUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }

    [Required]
    public long StateOrProvinceId { get; set; }

    [Required]
    public string CountryId { get; set; } = string.Empty;
}

// ----- Location lookups --------------------------------------------------------------------------------

public sealed record CountryLookupDto(string Id, string Name);

public sealed record StateOrProvinceLookupDto(long Id, string Name, string CountryId);

// ----- Promotions (cart rules / coupons) ---------------------------------------------------------------

public sealed record AdminCartRuleListItem(
    long Id, string Name, bool IsActive, bool IsCouponRequired, string? RuleToApply,
    decimal DiscountAmount, DateTimeOffset? StartOn, DateTimeOffset? EndOn, int CouponCount, int UsageCount);

public sealed record AdminCartRuleDetail(
    long Id, string Name, string? Description, bool IsActive, DateTimeOffset? StartOn, DateTimeOffset? EndOn,
    bool IsCouponRequired, string? RuleToApply, decimal DiscountAmount, decimal? MaxDiscountAmount,
    int? DiscountStep, int? UsageLimitPerCoupon, int? UsageLimitPerCustomer, string? CouponCode,
    IReadOnlyList<long> CategoryIds, IReadOnlyList<AdminProductLinkDto> Products);

public sealed class CartRuleUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? StartOn { get; set; }
    public DateTimeOffset? EndOn { get; set; }
    public bool IsCouponRequired { get; set; }

    /// <summary>"cart_fixed" or "by_percent" (the values CouponService understands).</summary>
    [Required]
    public string RuleToApply { get; set; } = "cart_fixed";

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }
    public int? DiscountStep { get; set; }
    public int? UsageLimitPerCoupon { get; set; }
    public int? UsageLimitPerCustomer { get; set; }

    /// <summary>The rule's coupon code; replaces the existing coupon on update, cleared when blank.</summary>
    public string? CouponCode { get; set; }

    public IList<long> CategoryIds { get; set; } = new List<long>();
    public IList<long> ProductIds { get; set; } = new List<long>();
}

public sealed record AdminCartRuleUsageDto(
    long Id, long CartRuleId, string CartRuleName, string? CouponCode, long UserId, string? UserEmail,
    long OrderId, DateTimeOffset CreatedOn);

// ----- Users & customer groups ---------------------------------------------------------------------

public sealed record AdminUserListItem(
    long Id, string? Email, string FullName, string? PhoneNumber, DateTimeOffset CreatedOn,
    bool IsDeleted, IReadOnlyList<string> Roles, IReadOnlyList<string> CustomerGroups);

public sealed record AdminUserDetail(
    long Id, string? Email, string FullName, string? PhoneNumber,
    IReadOnlyList<string> Roles, IReadOnlyList<long> CustomerGroupIds);

public sealed class AdminUserCreateRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public IList<long> CustomerGroupIds { get; set; } = new List<long>();
}

public sealed class AdminUserUpdateRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public IList<long> CustomerGroupIds { get; set; } = new List<long>();
}

public sealed record RoleDto(long Id, string? Name);

// ----- Customers (storefront shoppers; non-admin users) ----------------------------------------------

public sealed record AdminCustomerListItem(
    long Id, string? Email, string FullName, string? PhoneNumber, DateTimeOffset CreatedOn,
    bool IsDeleted, int OrderCount, decimal TotalSpent, IReadOnlyList<string> CustomerGroups);

public sealed record AdminCustomerDetail(
    long Id, string? Email, string FullName, string? PhoneNumber, IReadOnlyList<long> CustomerGroupIds);

public sealed class AdminCustomerCreateRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public IList<long> CustomerGroupIds { get; set; } = new List<long>();
}

public sealed class AdminCustomerUpdateRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public IList<long> CustomerGroupIds { get; set; } = new List<long>();
}

public sealed record AdminCustomerGroupDto(long Id, string Name, string? Description, bool IsActive);

public sealed class CustomerGroupUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

// ----- Reviews & comments moderation ------------------------------------------------------------------

public sealed record AdminReviewDto(
    long Id, string? Title, string? Comment, int Rating, string? ReviewerName, string? UserEmail,
    int Status, DateTimeOffset CreatedOn, long EntityId, string? EntityTypeId, string? ProductName);

public sealed record AdminCommentDto(
    long Id, string? CommentText, string? CommenterName, string? UserEmail,
    int Status, DateTimeOffset CreatedOn, long EntityId, string? EntityTypeId, long? ParentId);

/// <summary>1 = Pending, 5 = Approved, 8 = NotApproved (the old SimplCommerce enum values).</summary>
public sealed class ModerationStatusRequest
{
    [Required]
    public int Status { get; set; }
}

// ----- CMS: pages & menus --------------------------------------------------------------------------

public sealed record AdminPageDto(
    long Id, string Name, string Slug, string? Body, string? MetaTitle, string? MetaKeywords,
    string? MetaDescription, bool IsPublished, DateTimeOffset? PublishedOn, DateTimeOffset CreatedOn);

public sealed class PageUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Body { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed record AdminMenuItemDto(
    long Id, long MenuId, long? ParentId, string? Name, string? CustomLink, int DisplayOrder);

public sealed record AdminMenuDto(long Id, string Name, bool IsPublished, bool IsSystem,
    IReadOnlyList<AdminMenuItemDto> Items);

public sealed class MenuUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
}

public sealed class MenuItemUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? CustomLink { get; set; }
    public long? ParentId { get; set; }
    public int DisplayOrder { get; set; }
}

// ----- News -----------------------------------------------------------------------------------------

public sealed record AdminNewsCategoryDto(
    long Id, string Name, string Slug, string? Description, int DisplayOrder, bool IsPublished);

public sealed class NewsCategoryUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed record AdminNewsItemListItem(
    long Id, string Name, string Slug, bool IsPublished, DateTimeOffset CreatedOn, string? ThumbnailUrl);

public sealed record AdminNewsItemDetail(
    long Id, string Name, string Slug, string? ShortContent, string? FullContent,
    string? MetaTitle, string? MetaKeywords, string? MetaDescription,
    bool IsPublished, long? ThumbnailImageId, string? ThumbnailUrl, IReadOnlyList<long> CategoryIds);

public sealed class NewsItemUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? ShortContent { get; set; }
    public string? FullContent { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsPublished { get; set; } = true;
    public long? ThumbnailImageId { get; set; }
    public IList<long> CategoryIds { get; set; } = new List<long>();
}

// ----- Payments --------------------------------------------------------------------------------------

public sealed record AdminPaymentProviderDto(string Id, string Name, bool IsEnabled, string? AdditionalSettings);

public sealed class PaymentProviderUpdateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>Gateway-specific configuration JSON (API keys, fees, ...), like the old per-gateway config pages.</summary>
    public string? AdditionalSettings { get; set; }
}

public sealed record AdminPaymentDto(
    long Id, long OrderId, decimal Amount, decimal PaymentFee, string? PaymentMethod,
    string? GatewayTransactionId, int Status, DateTimeOffset CreatedOn);

// ----- Settings, countries/states, localization --------------------------------------------------------

public sealed record AppSettingDto(string Id, string? Value, string? Module, bool IsVisibleInCommonSettingPage);

public sealed class AppSettingUpdateRequest
{
    /// <summary>Key/value pairs to upsert.</summary>
    [Required]
    public Dictionary<string, string?> Settings { get; set; } = new();
}

public sealed record AdminCountryDto(
    string Id, string Name, string? Code3, bool IsBillingEnabled, bool IsShippingEnabled,
    bool IsCityEnabled, bool IsZipCodeEnabled, bool IsDistrictEnabled, int StatesCount);

public sealed class CountryUpsertRequest
{
    /// <summary>ISO code used as the primary key (e.g. "US"); required on create.</summary>
    public string? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Code3 { get; set; }
    public bool IsBillingEnabled { get; set; } = true;
    public bool IsShippingEnabled { get; set; } = true;
    public bool IsCityEnabled { get; set; } = true;
    public bool IsZipCodeEnabled { get; set; } = true;
    public bool IsDistrictEnabled { get; set; }
}

public sealed class StateOrProvinceUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }
    public string? Type { get; set; }
}

public sealed record CultureDto(string Id, string Name);

public sealed record AdminResourceDto(long Id, string Key, string? Value, string CultureId);

public sealed class ResourceUpsertRequest
{
    [Required]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    [Required]
    public string CultureId { get; set; } = string.Empty;
}

// ----- Shipments --------------------------------------------------------------------------------------

public sealed record AdminShipmentItemDto(long Id, long OrderItemId, long ProductId, string ProductName, int Quantity);

public sealed record AdminShipmentDto(
    long Id, long OrderId, string? TrackingNumber, long WarehouseId, string WarehouseName,
    DateTimeOffset CreatedOn, IReadOnlyList<AdminShipmentItemDto> Items);

public sealed class ShipmentItemRequest
{
    [Required]
    public long OrderItemId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public sealed class ShipmentCreateRequest
{
    [Required]
    public long OrderId { get; set; }

    [Required]
    public long WarehouseId { get; set; }

    public string? TrackingNumber { get; set; }

    [Required]
    public IList<ShipmentItemRequest> Items { get; set; } = new List<ShipmentItemRequest>();
}

// ----- Vendors -----------------------------------------------------------------------------------------

public sealed record AdminVendorDto(
    long Id, string Name, string Slug, string? Email, string? Description, bool IsActive);

public sealed class VendorUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

// ----- Contacts -----------------------------------------------------------------------------------------

public sealed record AdminContactDto(
    long Id, string? FullName, string? EmailAddress, string? PhoneNumber, string? Address,
    string? Content, long ContactAreaId, string ContactAreaName, DateTimeOffset CreatedOn);

public sealed record AdminContactAreaDto(long Id, string Name);

public sealed class ContactAreaUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

// ----- Activity log & search queries ----------------------------------------------------------------------

public sealed record AdminActivityDto(
    long Id, long ActivityTypeId, string ActivityTypeName, long UserId, long EntityId,
    string EntityTypeId, DateTimeOffset CreatedOn);

public sealed record AdminSearchQueryDto(string QueryText, int Count, DateTimeOffset LatestCreatedOn);

// ----- Product templates -----------------------------------------------------------------------------------

public sealed record AdminProductTemplateDto(
    long Id, string Name, IReadOnlyList<AdminProductAttributeDto> Attributes);

public sealed class ProductTemplateUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public IList<long> AttributeIds { get; set; } = new List<long>();
}

// ----- Dashboard / analytics -------------------------------------------------------------------------------

/// <summary>Aggregate decision-maker view for the admin landing page (GET /api/admin/dashboard/stats).
/// All order metrics cover the requested rolling window; stock metrics are current state.</summary>
public sealed record AdminDashboardDto(
    AdminDashboardKpisDto Kpis,
    IReadOnlyList<AdminTrendPointDto> RevenueTrend,
    IReadOnlyList<AdminStatusSliceDto> StatusFunnel,
    IReadOnlyList<AdminNameCountDto> PaymentMix,
    AdminChannelMixDto ChannelMix,
    AdminStockHealthDto StockHealth,
    IReadOnlyList<AdminTopProductDto> TopProducts,
    IReadOnlyList<AdminLowStockDto> LowStock,
    IReadOnlyList<OrderSummaryDto> ActionQueue);

/// <summary>Headline numbers. <see cref="Revenue"/> excludes canceled/refunded orders.</summary>
public sealed record AdminDashboardKpisDto(
    decimal Revenue, int Orders, decimal AvgOrderValue, int Products, int OutOfStock);

public sealed record AdminTrendPointDto(DateOnly Date, decimal Revenue, int Orders);

public sealed record AdminStatusSliceDto(int Status, string StatusName, int Count, decimal Total);

public sealed record AdminNameCountDto(string Name, int Count);

public sealed record AdminChannelMixDto(int Guest, int Account);

public sealed record AdminStockHealthDto(int OutOfStock, int Low, int Healthy, int TotalUnits);

public sealed record AdminTopProductDto(long ProductId, string Name, int Units, decimal Revenue);

public sealed record AdminLowStockDto(long ProductId, string Name, string? Sku, int Quantity, int Reserved);
