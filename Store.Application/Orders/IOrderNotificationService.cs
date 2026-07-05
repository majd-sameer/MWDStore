using Store.Domain;

namespace Store.Application.Orders;

/// <summary>
/// Enqueues the transactional emails for order-lifecycle events (placed, paid, shipped, cancelled) — one
/// copy to the customer and one to the store owner. Every method is best-effort: failures (missing
/// template, no default email account, DB error, etc.) are logged and swallowed so that an order
/// operation never fails because of a notification problem. Callers do not need to wrap these calls.
/// </summary>
public interface IOrderNotificationService
{
    /// <summary>Order successfully placed (right after <c>CreateOrderAsync</c> commits).</summary>
    Task NotifyOrderPlacedAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Order payment received (status transitioned to <c>OrderStatus.PaymentReceived</c>).</summary>
    Task NotifyOrderPaidAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Order shipped (status transitioned to <c>OrderStatus.Shipped</c>).</summary>
    Task NotifyOrderShippedAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Order cancelled (status transitioned to <c>OrderStatus.Canceled</c>).</summary>
    Task NotifyOrderCancelledAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Order refunded (full or partial). Default no-op so existing test/fake implementers need no change;
    /// the production <see cref="OrderNotificationService"/> overrides it to enqueue the
    /// <c>Order.Refunded</c> template. Best-effort like the rest — failures are swallowed by the impl.
    /// </summary>
    Task NotifyOrderRefundedAsync(Order order, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
