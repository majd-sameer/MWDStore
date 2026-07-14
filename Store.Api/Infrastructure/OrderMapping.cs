using Microsoft.EntityFrameworkCore;
using Store.Api.Models;
using Store.Application.Localization;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>Projects <see cref="Order"/> aggregates to API DTOs.</summary>
public static class OrderMapping
{
    /// <summary>Eager-loads everything <see cref="ToDetail"/> needs (items + products + addresses).</summary>
    public static IQueryable<Order> IncludeDetail(this IQueryable<Order> orders) => orders
        .Include(o => o.OrderItems).ThenInclude(i => i.Product)
        .Include(o => o.ShippingAddress)
        .Include(o => o.BillingAddress);

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

    /// <summary>
    /// Overlays the per-culture product names onto the order's line items (by product id), so a
    /// localized order view (e.g. English) shows the English product name instead of the Arabic base.
    /// No-op when <paramref name="cultureId"/> is null (base) or there are no items.
    /// </summary>
    public static async Task<OrderDetailDto> LocalizeItemsAsync(
        this OrderDetailDto detail, ILocalizationService localization, string? cultureId, CancellationToken cancellationToken)
    {
        if (cultureId is null || detail.Items.Count == 0)
        {
            return detail;
        }

        var ids = detail.Items.Select(i => i.ProductId).ToList();
        var overlay = await localization.GetOverlayAsync(LocalizedEntity.Product, ids, cultureId, cancellationToken);
        if (overlay.IsEmpty)
        {
            return detail;
        }

        var items = detail.Items
            .Select(i => i with
            {
                ProductName = overlay.Apply(i.ProductId, LocalizedProperty.Name, i.ProductName) ?? i.ProductName,
            })
            .ToList();
        return detail with { Items = items };
    }
}
