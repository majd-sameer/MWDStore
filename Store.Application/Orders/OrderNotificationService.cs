using System.Collections.Generic;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Messaging;
using Store.Data;
using Store.Domain;

namespace Store.Application.Orders;

/// <summary>
/// Default <see cref="IOrderNotificationService"/>. Renders order tokens once per event and enqueues two
/// copies through <see cref="IEmailQueueService"/>: <c>{TemplateName}</c> to the customer and
/// <c>{TemplateName}.OwnerCopy</c> to the store owner (<see cref="OwnerNotificationOptions.Email"/>). Both
/// enqueues are best-effort — a missing template, inactive template, or any other failure is logged and
/// swallowed so the caller's order operation always succeeds regardless of email health. A guest order with
/// no captured email simply skips the customer copy; the owner copy is still sent.
/// </summary>
public sealed class OrderNotificationService : IOrderNotificationService
{
    private const string OwnerCopySuffix = ".OwnerCopy";

    private readonly StoreDbContext _db;
    private readonly IEmailQueueService _emailQueue;
    private readonly OwnerNotificationOptions _ownerOptions;
    private readonly ILogger<OrderNotificationService> _logger;

    public OrderNotificationService(
        StoreDbContext db,
        IEmailQueueService emailQueue,
        OwnerNotificationOptions ownerOptions,
        ILogger<OrderNotificationService> logger)
    {
        _db = db;
        _emailQueue = emailQueue;
        _ownerOptions = ownerOptions;
        _logger = logger;
    }

    public Task NotifyOrderPlacedAsync(Order order, CancellationToken cancellationToken = default) =>
        NotifyAsync("Order.Placed", order, cancellationToken);

    public Task NotifyOrderPaidAsync(Order order, CancellationToken cancellationToken = default) =>
        NotifyAsync("Order.Paid", order, cancellationToken);

    public Task NotifyOrderShippedAsync(Order order, CancellationToken cancellationToken = default) =>
        NotifyAsync("Order.Shipped", order, cancellationToken);

    public Task NotifyOrderCancelledAsync(Order order, CancellationToken cancellationToken = default) =>
        NotifyAsync("Order.Cancelled", order, cancellationToken);

    public Task NotifyOrderRefundedAsync(Order order, CancellationToken cancellationToken = default) =>
        NotifyAsync("Order.Refunded", order, cancellationToken);

    private async Task NotifyAsync(string templateName, Order order, CancellationToken cancellationToken)
    {
        var (customerEmail, customerName) = await ResolveCustomerContactAsync(order, cancellationToken);
        var tokens = BuildTokens(order, customerName);

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            await SafeEnqueueAsync(templateName, tokens, customerEmail, customerName, order.Id, cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Skipping customer '{Template}' email for order {OrderId}: no email on file.", templateName, order.Id);
        }

        if (!string.IsNullOrWhiteSpace(_ownerOptions.Email))
        {
            await SafeEnqueueAsync(
                templateName + OwnerCopySuffix, tokens, _ownerOptions.Email, null, order.Id, cancellationToken);
        }
    }

    private async Task SafeEnqueueAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        string to,
        string? toName,
        long orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailQueue.EnqueueAsync(templateName, tokens, to, toName, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "Failed to enqueue '{Template}' email for order {OrderId}.", templateName, orderId);
        }
    }

    /// <summary>
    /// Guest orders carry their contact email directly on <see cref="Order.GuestEmail"/>; signed-in orders
    /// resolve it from the customer's account. Falls back to the already-loaded <see cref="Order.Customer"/>
    /// navigation when present to avoid an extra round-trip.
    /// </summary>
    private async Task<(string? Email, string? Name)> ResolveCustomerContactAsync(
        Order order, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(order.GuestEmail))
        {
            return (order.GuestEmail, order.ShippingAddress?.ContactName ?? order.BillingAddress?.ContactName);
        }

        if (order.Customer is { Email.Length: > 0 })
        {
            return (order.Customer.Email, order.Customer.FullName);
        }

        var customer = await _db.Set<User>()
            .Where(u => u.Id == order.CustomerId)
            .Select(u => new { u.Email, u.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        return (customer?.Email, customer?.FullName);
    }

    private static Dictionary<string, string?> BuildTokens(Order order, string? customerName) => new()
    {
        ["Order.Number"] = order.TrackingNumber ?? order.Id.ToString(CultureInfo.InvariantCulture),
        ["Order.Total"] = order.OrderTotal.ToString("0.00", CultureInfo.InvariantCulture),
        ["Order.Status"] = OrderStatusName(order.OrderStatus),
        ["Customer.Name"] = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName,
        ["Order.TrackingCode"] = order.TrackingNumber
    };

    /// <summary>
    /// Display name for the <c>%Order.Status%</c> token. Mirrors <c>Store.Api.Infrastructure.OrderStatusNames</c>
    /// (kept separate since the Application layer cannot reference Store.Api).
    /// </summary>
    private static string OrderStatusName(int status) => status switch
    {
        OrderStatus.New => "New",
        OrderStatus.OnHold => "On Hold",
        OrderStatus.PendingPayment => "Pending Payment",
        OrderStatus.PaymentReceived => "Payment Received",
        OrderStatus.PaymentFailed => "Payment Failed",
        OrderStatus.Invoiced => "Invoiced",
        OrderStatus.Shipping => "Shipping",
        OrderStatus.Shipped => "Shipped",
        OrderStatus.Complete => "Complete",
        OrderStatus.Canceled => "Canceled",
        OrderStatus.Refunded => "Refunded",
        OrderStatus.Closed => "Closed",
        _ => status.ToString(CultureInfo.InvariantCulture)
    };
}
