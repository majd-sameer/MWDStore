using System.ComponentModel.DataAnnotations;
using Store.Application.Orders;

namespace Store.Api.Models;


public sealed record CategoryDto(
    long Id, string Name, string Slug, long? ParentId, int DisplayOrder, bool IncludeInMenu);

public sealed record BrandDto(long Id, string Name, string Slug);


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

    public string? CouponCode { get; set; }
}

public sealed record ShippingOptionDto(string? Id, string Name, decimal Price);

public sealed class PlaceOrderRequest
{
    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    public AddressDto? BillingAddress { get; set; }

    [Required]
    public string ShippingMethodName { get; set; } = string.Empty;

    public string? PaymentMethod { get; set; }

    public decimal PaymentFeeAmount { get; set; }

    public string? CouponCode { get; set; }

    public string? OrderNote { get; set; }

    public bool IsProductPriceIncludeTax { get; set; }
}

public sealed class GuestCartLine
{
    [Required]
    public long ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}

public sealed class GuestShippingOptionsRequest
{
    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<GuestCartLine> Items { get; set; } = [];
}

public sealed class GuestPlaceOrderRequest
{
      public string? Email { get; set; }

    [Required]
    [MinLength(1)]
    public List<GuestCartLine> Items { get; set; } = [];

    [Required]
    public AddressDto ShippingAddress { get; set; } = new();

    public AddressDto? BillingAddress { get; set; }

    [Required]
    public string ShippingMethodName { get; set; } = string.Empty;

    public string? PaymentMethod { get; set; }

    public decimal PaymentFeeAmount { get; set; }

    public string? OrderNote { get; set; }

    public bool IsProductPriceIncludeTax { get; set; }
}



public sealed record OrderItemDto(
    long Id, long ProductId, string ProductName, decimal ProductPrice, int Quantity,
    decimal DiscountAmount, decimal TaxAmount, decimal TaxPercent);

public sealed record OrderAddressDto(
    string? ContactName, string? Phone, string? AddressLine1, string? AddressLine2,
    string? City, string? ZipCode, long StateOrProvinceId, string? CountryId);

public sealed record OrderSummaryDto(
    long Id, string? TrackingNumber, DateTimeOffset CreatedOn, int OrderStatus, string OrderStatusName,
    decimal OrderTotal, int ItemCount, string? PaymentMethod = null,
    string? CreatedBy = null, string? ModifiedBy = null);


/// <summary>
/// What the storefront should do after a "pay again" preflight: start a new payment for the order
/// (<paramref name="CanPay"/>), or go to the cart, where the order's lines now sit and
/// <paramref name="UnavailableItems"/> are shown as no longer buyable (<paramref name="MovedToCart"/>).
/// </summary>
public sealed record OrderRetryPaymentDto(
    long OrderId, bool CanPay, bool MovedToCart, IReadOnlyList<OrderRetryItemDto> UnavailableItems);

/// <summary>A line that could not be re-ordered. <paramref name="Reason"/> is <c>out-of-stock</c> or <c>unavailable</c>.</summary>
public sealed record OrderRetryItemDto(
    long ProductId, string ProductName, int RequestedQuantity, int AvailableQuantity, string Reason);

public sealed record OrderTrackingDto(
    long Id, string? TrackingNumber, DateTimeOffset CreatedOn, int OrderStatus, string OrderStatusName,
    decimal OrderTotal, int ItemCount, string? ShippingMethod, string? PaymentMethod,
    IReadOnlyList<OrderTrackingEventDto> History,
    OrderDetailDto Detail);

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

    string? GuestEmail = null);
