using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;
using Store.Application.Orders;
using Store.Application.Payments.Stripe;
using Store.Data;
using Store.Domain;

namespace Store.Application.Payments;

/// <summary>
/// Default <see cref="IRefundService"/>. Refund execution is deliberately ordered so money correctness and
/// idempotency come first: validate → (idempotency short-circuit) → execute against the provider → persist
/// the refund + status transitions in a single <c>SaveChanges</c> → best-effort notify. A notification
/// failure can never undo a committed refund, and a retried request (same idempotency key) never issues a
/// second provider refund.
/// </summary>
public sealed class RefundService : IRefundService
{
    /// <summary>Provider id of the Stripe gateway (matches the seeded <c>PaymentProvider</c> row).</summary>
    private const string Stripe = "Stripe";

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IStripeClient _stripe;
    private readonly IOrderNotificationService _notifications;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        StoreDbContext db,
        TimeProvider timeProvider,
        IStripeClient stripe,
        IOrderNotificationService notifications,
        ILogger<RefundService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _stripe = stripe;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Result<RefundResult>> RefundAsync(
        RefundRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders
            .Include(o => o.Payments).ThenInclude(p => p.Refunds)
            .Include(o => o.OrderHistories)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            return Result.Fail<RefundResult>("Order not found.");
        }

        // The captured payment to refund: the settled row (a partial refund leaves it PartiallyRefunded).
        var payment = order.Payments
            .Where(p => p.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();

        if (payment == null)
        {
            return Result.Fail<RefundResult>("This order has no captured payment to refund.");
        }

        // Idempotency: a retry carrying the same key returns the original refund, no second provider call.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = payment.Refunds
                .FirstOrDefault(r => r.IdempotencyKey == request.IdempotencyKey);
            if (existing != null)
            {
                var totalSoFar = payment.Refunds.Sum(r => r.Amount);
                return Result.Ok(new RefundResult(
                    existing.Id, order.Id, payment.Id, existing.Amount, totalSoFar,
                    payment.Status, payment.Status == PaymentStatus.Refunded, existing.ProviderRefundId,
                    AlreadyProcessed: true));
            }
        }

        var alreadyRefunded = payment.Refunds.Sum(r => r.Amount);
        var remaining = payment.Amount - alreadyRefunded;
        if (remaining <= 0m)
        {
            return Result.Fail<RefundResult>("This payment has already been fully refunded.");
        }

        var amount = request.Amount ?? remaining;
        if (amount <= 0m)
        {
            return Result.Fail<RefundResult>("Refund amount must be greater than zero.");
        }

        if (amount > remaining)
        {
            return Result.Fail<RefundResult>(
                $"Refund amount {amount:0.00} exceeds the refundable balance {remaining:0.00}.");
        }

        // Execute against the provider (Stripe) or record a manual/offline refund (CoD/other).
        string? providerRefundId = null;
        var isManual = true;

        if (string.Equals(payment.PaymentMethod, Stripe, StringComparison.OrdinalIgnoreCase))
        {
            var stripeResult = await ExecuteStripeRefundAsync(payment, amount, request, cancellationToken);
            if (!stripeResult.Success)
            {
                return Result.Fail<RefundResult>(stripeResult.Error!);
            }

            providerRefundId = stripeResult.Value;
            isManual = false;
        }

        var now = _timeProvider.GetUtcNow();
        var refund = new Refund
        {
            PaymentId = payment.Id,
            OrderId = order.Id,
            Amount = amount,
            CreatedOn = now,
            CreatedById = request.RequestedByUserId,
            Reason = request.Reason,
            ProviderRefundId = providerRefundId,
            IsManual = isManual,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };
        _db.Set<Refund>().Add(refund);

        var totalRefunded = alreadyRefunded + amount;
        var fullyRefunded = totalRefunded >= payment.Amount;

        payment.Status = fullyRefunded ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        payment.LatestUpdatedOn = now;

        if (fullyRefunded)
        {
            // Mirror the cancel path's status write, but DO NOT restock: a refund is a financial reversal,
            // not a return of goods. Restock stays tied to CancelOrderAsync (an explicit non-fulfillment).
            SetOrderStatus(order, OrderStatus.Refunded, now, request.RequestedByUserId,
                $"Order refunded ({totalRefunded:0.00}).");
        }
        else
        {
            order.LatestUpdatedOn = now;
            order.OrderHistories.Add(new OrderHistory
            {
                OrderId = order.Id,
                OldStatus = order.OrderStatus,
                NewStatus = order.OrderStatus,
                Note = $"Partial refund issued ({amount:0.00}); total refunded {totalRefunded:0.00}.",
                CreatedOn = now,
                CreatedById = request.RequestedByUserId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Best-effort: a notification failure must never roll back a committed refund.
        await _notifications.NotifyOrderRefundedAsync(order, cancellationToken);

        return Result.Ok(new RefundResult(
            refund.Id, order.Id, payment.Id, amount, totalRefunded,
            payment.Status, fullyRefunded, providerRefundId, AlreadyProcessed: false));
    }

    /// <summary>
    /// Resolves the payment's PaymentIntent from its stored Checkout session id and issues the Stripe
    /// refund, forwarding the idempotency key so a retry does not double-refund at the gateway.
    /// Returns the Stripe refund id on success.
    /// </summary>
    private async Task<Result<string>> ExecuteStripeRefundAsync(
        Payment payment, decimal amount, RefundRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.GatewayTransactionId))
        {
            return Result.Fail<string>("Stripe payment has no gateway reference to refund.");
        }

        var provider = await _db.PaymentProviders
            .FirstOrDefaultAsync(p => p.Id == Stripe, cancellationToken);
        if (provider == null)
        {
            return Result.Fail<string>("Stripe provider is not configured.");
        }

        var settings = GatewaySettings.Parse(provider.AdditionalSettings);
        if (string.IsNullOrWhiteSpace(settings.StripeSecretKey))
        {
            return Result.Fail<string>("Stripe secret key is not configured.");
        }

        try
        {
            var session = await _stripe.GetCheckoutSessionAsync(
                payment.GatewayTransactionId, settings.StripeSecretKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(session.PaymentIntentId))
            {
                return Result.Fail<string>("Stripe payment has no PaymentIntent to refund.");
            }

            var refund = await _stripe.CreateRefundAsync(
                new StripeRefundRequest(
                    PaymentIntentId: session.PaymentIntentId,
                    Amount: amount,
                    Currency: settings.Currency,
                    SecretKey: settings.StripeSecretKey,
                    Reason: request.Reason,
                    IdempotencyKey: BuildStripeIdempotencyKey(payment.Id, request)),
                cancellationToken);

            return Result.Ok(refund.Id);
        }
        catch (global::Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for order {OrderId}, payment {PaymentId}.",
                payment.OrderId, payment.Id);
            return Result.Fail<string>("Could not process the refund with Stripe.");
        }
    }

    /// <summary>
    /// Derives the key forwarded to Stripe. Uses the caller's key when present so retries dedupe at the
    /// gateway; otherwise scopes a key to this payment + amount to guard an accidental immediate repeat.
    /// </summary>
    private static string BuildStripeIdempotencyKey(long paymentId, RefundRequest request) =>
        string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"refund-{paymentId}-{request.Amount?.ToString("0.00") ?? "full"}"
            : $"refund-{paymentId}-{request.IdempotencyKey}";

    private void SetOrderStatus(Order order, int newStatus, DateTimeOffset now, long userId, string note)
    {
        var oldStatus = order.OrderStatus;
        order.OrderStatus = newStatus;
        order.LatestUpdatedOn = now;
        order.LatestUpdatedById = userId;
        order.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = note,
            CreatedOn = now,
            CreatedById = userId
        });
    }
}
