using Store.Application.Common;
using Store.Domain;

namespace Store.Application.Orders;

/// <summary>
/// Turns a <c>Checkout</c> snapshot into an <c>Order</c> with authoritative per-line and
/// rolled-up totals.
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
    Task CancelOrderAsync(Order order, string? note = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Preflight for "pay again" on an order whose payment failed. Checks every line is still
    /// orderable, then either clears the shopper to start a new payment for the same order
    /// (<see cref="OrderRetryResult.CanPay"/>) or, when anything is no longer available, returns the
    /// whole order to the cart (<see cref="OrderRetryResult.MovedToCart"/>) and cancels it — so the
    /// shopper lands on a cart holding what they can still buy, with the rest listed as unavailable.
    /// </summary>
    Task<Result<OrderRetryResult>> RetryPaymentAsync(
        long orderId, long customerId, CancellationToken cancellationToken = default);

    /// <summary>Standalone tax estimate over a checkout's items (used before the order exists).</summary>
    Task<decimal> GetTaxAsync(
        Guid checkoutId, string? countryId, long stateOrProvinceId, string? zipCode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a "pay again" preflight. Exactly one of <see cref="CanPay"/> and
/// <see cref="MovedToCart"/> is true.
/// </summary>
/// <param name="OrderId">The order the shopper asked to retry.</param>
/// <param name="CanPay">Everything is still orderable — start a new payment for this order.</param>
/// <param name="MovedToCart">
/// Something was no longer orderable, so every line went back to the cart and the order was canceled
/// (its stock returned). The shopper should be sent to the cart.
/// </param>
/// <param name="UnavailableItems">The lines that could not be re-ordered; empty when <c>CanPay</c>.</param>
public sealed record OrderRetryResult(
    long OrderId,
    bool CanPay,
    bool MovedToCart,
    IReadOnlyList<OrderRetryItem> UnavailableItems);

/// <summary>A line that can no longer be ordered, with what is actually left.</summary>
/// <param name="Reason">
/// <c>out-of-stock</c> (some or all of the quantity is gone) or <c>unavailable</c> (the product was
/// unpublished, deleted, or is no longer sold).
/// </param>
public sealed record OrderRetryItem(
    long ProductId,
    string ProductName,
    int RequestedQuantity,
    int AvailableQuantity,
    string Reason);
