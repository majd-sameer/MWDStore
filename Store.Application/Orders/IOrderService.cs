using Store.Application.Common;
using Store.Domain;

namespace Store.Application.Orders;

/// <summary>
/// Port of SimplCommerce's <c>IOrderService</c> order-creation path: turns a <c>Checkout</c> snapshot into
/// an <c>Order</c> with authoritative per-line and rolled-up totals.
/// </summary>
public interface IOrderService
{
    Task<Result<Order>> CreateOrderAsync(
        Guid checkoutId,
        string? paymentMethod,
        decimal paymentFeeAmount,
        string shippingMethodName,
        OrderAddressInfo billingAddress,
        OrderAddressInfo shippingAddress,
        int orderStatus = OrderStatus.New,
        string? guestEmail = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels an order and restocks each stock-tracked line.</summary>
    Task CancelOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Standalone tax estimate over a checkout's items (used before the order exists).</summary>
    Task<decimal> GetTaxAsync(
        Guid checkoutId, string? countryId, long stateOrProvinceId, string? zipCode,
        CancellationToken cancellationToken = default);
}
