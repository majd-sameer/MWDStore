using System.ComponentModel.DataAnnotations;
using Store.Application.Orders;

namespace Store.Api.Models;

// ----- Catalog ------------------------------------------------------------------------------------

/// <summary>A storefront category node (flattened; <see cref="ParentId"/> conveys the tree).</summary>
public sealed record CategoryDto(
    long Id, string Name, string Slug, long? ParentId, int DisplayOrder, bool IncludeInMenu);

/// <summary>A storefront brand.</summary>
public sealed record BrandDto(long Id, string Name, string Slug);

// ----- Cart ---------------------------------------------------------------------------------------

public sealed class AddToCartRequest
{
    [Required]
    public long ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}

public sealed class UpdateCartItemRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

// ----- Checkout -----------------------------------------------------------------------------------

/// <summary>The address an order ships/bills to (maps to <see cref="OrderAddressInfo"/>).</summary>
public sealed class AddressDto
{
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public long? DistrictId { get; set; }
    public long StateOrProvinceId { get; set; }
    public string? CountryId { get; set; }

    public OrderAddressInfo ToOrderAddressInfo() => new()
    {
        ContactName = ContactName,
        Phone = Phone,
        AddressLine1 = AddressLine1,
        AddressLine2 = AddressLine2,
        City = City,
        ZipCode = ZipCode,
        DistrictId = DistrictId,
        StateOrProvinceId = StateOrProvinceId,
        CountryId = CountryId
    };
}

public sealed class ShippingOptionsRequest
{
    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    /// <summary>Optional coupon — does not affect shipping rates but kept for parity with the cart total.</summary>
    public string? CouponCode { get; set; }
}

/// <summary>A selectable shipping option at checkout. <c>Id</c> is the provider id (carrier) so the
/// storefront can localize the label; <c>Name</c> is the carrier's display name.</summary>
public sealed record ShippingOptionDto(string? Id, string Name, decimal Price);

public sealed class PlaceOrderRequest
{
    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    /// <summary>Defaults to the shipping address when omitted.</summary>
    public AddressDto? BillingAddress { get; set; }

    [Required]
    public string ShippingMethodName { get; set; } = string.Empty;

    public string? PaymentMethod { get; set; }

    public decimal PaymentFeeAmount { get; set; }

    public string? CouponCode { get; set; }

    public string? OrderNote { get; set; }

    /// <summary>Whether the catalog prices already include tax (SimplCommerce's checkout flag).</summary>
    public bool IsProductPriceIncludeTax { get; set; }
}

/// <summary>A single line a guest is checking out (guests have no server cart — they post the lines).</summary>
public sealed class GuestCartLine
{
    [Required]
    public long ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}

/// <summary>Guest variant of <see cref="ShippingOptionsRequest"/> — carries the cart lines in the body.</summary>
public sealed class GuestShippingOptionsRequest
{
    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<GuestCartLine> Items { get; set; } = [];
}

/// <summary>Guest variant of <see cref="PlaceOrderRequest"/> — carries the cart lines and contact email.</summary>
public sealed class GuestPlaceOrderRequest
{
    /// <summary>Optional contact email. When supplied it is the order's tracking secret; an empty/blank
    /// value is accepted and the controller synthesizes a unique placeholder. No <c>[EmailAddress]</c>
    /// attribute here on purpose — it rejects an empty string, which would 400 an emailless guest order;
    /// a supplied email's format is validated client-side before submit.</summary>
    public string? Email { get; set; }

    [Required]
    [MinLength(1)]
    public List<GuestCartLine> Items { get; set; } = [];

    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    /// <summary>Defaults to the shipping address when omitted.</summary>
    public AddressDto? BillingAddress { get; set; }

    [Required]
    public string ShippingMethodName { get; set; } = string.Empty;

    public string? PaymentMethod { get; set; }

    public decimal PaymentFeeAmount { get; set; }

    public string? OrderNote { get; set; }

    /// <summary>Whether the catalog prices already include tax (SimplCommerce's checkout flag).</summary>
    public bool IsProductPriceIncludeTax { get; set; }
}

// ----- Orders -------------------------------------------------------------------------------------

public sealed record OrderItemDto(
    long Id, long ProductId, string ProductName, decimal ProductPrice, int Quantity,
    decimal DiscountAmount, decimal TaxAmount, decimal TaxPercent);

public sealed record OrderAddressDto(
    string? ContactName, string? Phone, string? AddressLine1, string? AddressLine2,
    string? City, string? ZipCode, long StateOrProvinceId, string? CountryId);

public sealed record OrderSummaryDto(
    long Id, string? TrackingNumber, DateTimeOffset CreatedOn, int OrderStatus, string OrderStatusName,
    decimal OrderTotal, int ItemCount);

/// <summary>
/// Public order-status view returned by the anonymous tracking lookup. The email match guarding the
/// lookup acts as the customer's shared secret, so the full order <see cref="Detail"/> (the same view
/// the signed-in customer sees) is included alongside the status timeline.
/// </summary>
public sealed record OrderTrackingDto(
    long Id, string? TrackingNumber, DateTimeOffset CreatedOn, int OrderStatus, string OrderStatusName,
    decimal OrderTotal, int ItemCount, string? ShippingMethod, string? PaymentMethod,
    IReadOnlyList<OrderTrackingEventDto> History,
    OrderDetailDto Detail);

/// <summary>A single status-change milestone for the tracking timeline (status + when, no internal notes).</summary>
public sealed record OrderTrackingEventDto(int Status, string StatusName, DateTimeOffset CreatedOn);

public sealed record OrderDetailDto(
    long Id,
    string? TrackingNumber,
    DateTimeOffset CreatedOn,
    int OrderStatus,
    string OrderStatusName,
    long CustomerId,
    string? CouponCode,
    decimal SubTotal,
    decimal SubTotalWithDiscount,
    decimal DiscountAmount,
    decimal TaxAmount,
    string? ShippingMethod,
    decimal ShippingFeeAmount,
    string? PaymentMethod,
    decimal PaymentFeeAmount,
    decimal OrderTotal,
    string? OrderNote,
    OrderAddressDto? ShippingAddress,
    OrderAddressDto? BillingAddress,
    IReadOnlyList<OrderItemDto> Items,
    /// <summary>The guest's contact email / order secret. Populated for the placing client only;
    /// nulled in the public tracking response so it isn't leaked by a tracking-number lookup.</summary>
    string? GuestEmail = null,
    /// <summary>Admin-only payment/refund rollup. Set only by the admin order-detail mapping; null on
    /// storefront/public order DTOs.</summary>
    PaymentSummaryDto? PaymentSummary = null);

/// <summary>Admin order-detail payment rollup: the captured payment's amount, how much of it has been
/// refunded, and the remaining refundable balance (captured minus already refunded).</summary>
public sealed record PaymentSummaryDto(decimal CapturedTotal, decimal RefundedTotal, decimal Refundable);
