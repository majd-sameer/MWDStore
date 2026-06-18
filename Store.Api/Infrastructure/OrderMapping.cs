using Store.Api.Models;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>Projects <see cref="Order"/> aggregates to API DTOs.</summary>
public static class OrderMapping
{
    public static OrderSummaryDto ToSummary(this Order order) => new(
        order.Id,
        order.TrackingNumber,
        order.CreatedOn,
        order.OrderStatus,
        OrderStatusNames.For(order.OrderStatus),
        order.OrderTotal,
        order.OrderItems.Sum(i => i.Quantity));

    /// <summary>Requires <c>OrderItems.Product</c>, <c>BillingAddress</c> and <c>ShippingAddress</c> to be loaded.</summary>
    public static OrderDetailDto ToDetail(this Order order) => new(
        order.Id,
        order.TrackingNumber,
        order.CreatedOn,
        order.OrderStatus,
        OrderStatusNames.For(order.OrderStatus),
        order.CustomerId,
        order.CouponCode,
        order.SubTotal,
        order.SubTotalWithDiscount,
        order.DiscountAmount,
        order.TaxAmount,
        order.ShippingMethod,
        order.ShippingFeeAmount,
        order.PaymentMethod,
        order.PaymentFeeAmount,
        order.OrderTotal,
        order.OrderNote,
        order.ShippingAddress?.ToDto(),
        order.BillingAddress?.ToDto(),
        order.OrderItems
            .Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.ProductPrice,
                i.Quantity,
                i.DiscountAmount,
                i.TaxAmount,
                i.TaxPercent))
            .ToList(),
        order.GuestEmail);

    private static OrderAddressDto ToDto(this OrderAddress a) => new(
        a.ContactName, a.Phone, a.AddressLine1, a.AddressLine2,
        a.City, a.ZipCode, a.StateOrProvinceId, a.CountryId);
}
