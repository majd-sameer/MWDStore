using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Messaging;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Application.Payments.Stripe;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Refund pipeline: validation (unpaid / over-refund), full + partial-then-remainder flows, idempotency
/// (a retried request never double-refunds), the Stripe path (provider called with the right amount) vs the
/// manual/CoD path (no external call), and the guarantee that a notification failure never fails a
/// committed refund.
/// </summary>
public class RefundServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private const long OrderId = 1;
    private const long PaymentId = 1;

    /// <summary>Records refund calls; the four lifecycle methods are irrelevant here (default no-op base).</summary>
    private sealed class RecordingNotificationService : IOrderNotificationService
    {
        public List<long> RefundedOrderIds { get; } = [];

        public Task NotifyOrderPlacedAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrderPaidAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrderShippedAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrderCancelledAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyOrderRefundedAsync(Order order, CancellationToken cancellationToken = default)
        {
            RefundedOrderIds.Add(order.Id);
            return Task.CompletedTask;
        }
    }

    /// <summary>Fake Stripe client that records refund/session calls. Only the refund slice is exercised.</summary>
    private sealed class FakeStripeClient : IStripeClient
    {
        public List<StripeRefundRequest> Refunds { get; } = [];
        public int SessionLookups { get; private set; }
        public string PaymentIntentId { get; set; } = "pi_test_1";

        public Task<StripeSession> GetCheckoutSessionAsync(string sessionId, string secretKey, CancellationToken cancellationToken = default)
        {
            SessionLookups++;
            return Task.FromResult(new StripeSession(sessionId, null, true, PaymentIntentId, OrderId));
        }

        public Task<StripeRefund> CreateRefundAsync(StripeRefundRequest request, CancellationToken cancellationToken = default)
        {
            Refunds.Add(request);
            return Task.FromResult(new StripeRefund("re_" + Refunds.Count, "succeeded"));
        }

        public Task<StripeSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public StripeSession? ReadCheckoutSessionFromWebhook(string payload, string signatureHeader, string webhookSecret) =>
            throw new NotSupportedException();
    }

    private static (StoreDbContext Db, FakeStripeClient Stripe, RecordingNotificationService Notifications, RefundService Service)
        NewService(string paymentMethod, int paymentStatus = PaymentStatus.Succeeded, decimal amount = 42m, string? guestEmail = null)
    {
        var db = TestDb.New();
        db.PaymentProviders.Add(new PaymentProvider { Id = "CoD", Name = "Cash on Delivery", IsEnabled = true });
        db.PaymentProviders.Add(new PaymentProvider
        {
            Id = "Stripe",
            Name = "Stripe",
            IsEnabled = true,
            AdditionalSettings = "{\"secretKey\":\"sk_test_x\",\"publicKey\":\"pk_test_x\",\"currency\":\"jod\"}"
        });
        db.Users.Add(new User { Id = 1, UserName = "buyer@example.com", Email = "buyer@example.com", FullName = "Buyer" });
        db.Orders.Add(new Order
        {
            Id = OrderId,
            CustomerId = 1,
            OrderStatus = OrderStatus.PaymentReceived,
            TrackingNumber = "T-1",
            OrderTotal = amount,
            GuestEmail = guestEmail
        });
        db.Payments.Add(new Payment
        {
            Id = PaymentId,
            OrderId = OrderId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            GatewayTransactionId = paymentMethod == "Stripe" ? "cs_test_1" : null,
            Status = paymentStatus,
            CreatedOn = Now,
            LatestUpdatedOn = Now
        });
        db.SaveChanges();

        var stripe = new FakeStripeClient();
        var notifications = new RecordingNotificationService();
        var service = new RefundService(
            db, new FixedTimeProvider(Now), stripe, notifications, NullLogger<RefundService>.Instance);
        return (db, stripe, notifications, service);
    }

    private static RefundRequest Req(decimal? amount, string? key = null, string? reason = "customer request") =>
        new(OrderId, amount, reason, RequestedByUserId: 99, IdempotencyKey: key);

    [Fact]
    public async Task FullRefund_Manual_RefundsWholeAmount_AndMovesOrderToRefunded()
    {
        var (db, stripe, notifications, service) = NewService("CoD");

        var result = await service.RefundAsync(Req(amount: null));

        Assert.True(result.Success);
        var r = result.Value!;
        Assert.Equal(42m, r.Amount);
        Assert.True(r.FullyRefunded);
        Assert.Equal(PaymentStatus.Refunded, r.PaymentStatus);
        Assert.Equal(PaymentStatus.Refunded, db.Payments.Single().Status);
        Assert.Equal(OrderStatus.Refunded, db.Orders.Single().OrderStatus);
        Assert.Empty(stripe.Refunds);                       // manual: no external call
        Assert.True(db.Refunds.Single().IsManual);
        Assert.Equal(new[] { OrderId }, notifications.RefundedOrderIds);
    }

    [Fact]
    public async Task PartialRefund_ThenRemainder_TotalsCapturedAmount_AndFinallyRefunded()
    {
        var (db, _, _, service) = NewService("CoD");

        var first = await service.RefundAsync(Req(amount: 10m));
        Assert.True(first.Success);
        Assert.False(first.Value!.FullyRefunded);
        Assert.Equal(PaymentStatus.PartiallyRefunded, first.Value!.PaymentStatus);
        Assert.Equal(PaymentStatus.PartiallyRefunded, db.Payments.Single().Status);
        // Partial refund does not change the order status.
        Assert.Equal(OrderStatus.PaymentReceived, db.Orders.Single().OrderStatus);

        var second = await service.RefundAsync(Req(amount: 32m));
        Assert.True(second.Success);
        Assert.True(second.Value!.FullyRefunded);
        Assert.Equal(42m, second.Value!.TotalRefunded);
        Assert.Equal(PaymentStatus.Refunded, db.Payments.Single().Status);
        Assert.Equal(OrderStatus.Refunded, db.Orders.Single().OrderStatus);
        Assert.Equal(2, db.Refunds.Count());
    }

    [Fact]
    public async Task OverRefund_IsRejected_AndRecordsNothing()
    {
        var (db, _, _, service) = NewService("CoD");

        // Already refunded 40 of 42; a further 10 would exceed the 2 remaining.
        Assert.True((await service.RefundAsync(Req(amount: 40m))).Success);
        var over = await service.RefundAsync(Req(amount: 10m));

        Assert.False(over.Success);
        Assert.Contains("exceeds", over.Error);
        Assert.Single(db.Refunds);                          // only the first refund persisted
        Assert.Equal(PaymentStatus.PartiallyRefunded, db.Payments.Single().Status);
    }

    [Fact]
    public async Task UnpaidOrder_IsRejected()
    {
        // Payment never captured (still pending) — nothing to refund.
        var (db, _, _, service) = NewService("CoD", paymentStatus: PaymentStatus.PendingExecution);

        var result = await service.RefundAsync(Req(amount: null));

        Assert.False(result.Success);
        Assert.Contains("no captured payment", result.Error);
        Assert.Empty(db.Refunds);
    }

    [Fact]
    public async Task IdempotentRetry_SameKey_DoesNotDoubleRefund()
    {
        var (db, stripe, _, service) = NewService("Stripe");

        var first = await service.RefundAsync(Req(amount: 15m, key: "abc-123"));
        var second = await service.RefundAsync(Req(amount: 15m, key: "abc-123"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(first.Value!.AlreadyProcessed);
        Assert.True(second.Value!.AlreadyProcessed);
        Assert.Equal(first.Value!.RefundId, second.Value!.RefundId);
        Assert.Single(db.Refunds);                          // exactly one refund row
        Assert.Single(stripe.Refunds);                      // Stripe called exactly once
        Assert.Equal(15m, db.Refunds.Single().Amount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, db.Payments.Single().Status);
    }

    [Fact]
    public async Task StripeRefund_CallsProviderWithRightAmountAndPaymentIntent()
    {
        var (_, stripe, _, service) = NewService("Stripe");

        var result = await service.RefundAsync(Req(amount: 25m));

        Assert.True(result.Success);
        var call = Assert.Single(stripe.Refunds);
        Assert.Equal(25m, call.Amount);
        Assert.Equal("pi_test_1", call.PaymentIntentId);
        Assert.Equal("jod", call.Currency);
        Assert.Equal("re_1", result.Value!.ProviderRefundId);
        Assert.Equal(1, stripe.SessionLookups);
    }

    [Fact]
    public async Task ManualRefund_RecordsNoProviderRefundId_AndNoStripeCall()
    {
        var (db, stripe, _, service) = NewService("CoD");

        var result = await service.RefundAsync(Req(amount: 42m));

        Assert.True(result.Success);
        Assert.Null(result.Value!.ProviderRefundId);
        Assert.True(db.Refunds.Single().IsManual);
        Assert.Empty(stripe.Refunds);
        Assert.Equal(0, stripe.SessionLookups);
    }

    /// <summary>An email/notification failure must never roll back a committed refund.</summary>
    private sealed class ThrowingEmailQueueService : IEmailQueueService
    {
        public Task<long> EnqueueAsync(
            string templateName, IReadOnlyDictionary<string, string?> tokens, string to, string? toName = null,
            long? emailAccountId = null, int priority = 0, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("email template missing");

        public Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    [Fact]
    public async Task NotificationFailure_DoesNotFailRefund()
    {
        var db = TestDb.New();
        db.PaymentProviders.Add(new PaymentProvider { Id = "CoD", Name = "Cash on Delivery", IsEnabled = true });
        db.Users.Add(new User { Id = 1, UserName = "buyer@example.com", Email = "buyer@example.com", FullName = "Buyer" });
        db.Orders.Add(new Order
        {
            Id = OrderId, CustomerId = 1, OrderStatus = OrderStatus.PaymentReceived,
            TrackingNumber = "T-1", OrderTotal = 42m, GuestEmail = "buyer@example.com"
        });
        db.Payments.Add(new Payment
        {
            Id = PaymentId, OrderId = OrderId, Amount = 42m, PaymentMethod = "CoD",
            Status = PaymentStatus.Succeeded, CreatedOn = Now, LatestUpdatedOn = Now
        });
        db.SaveChanges();

        // Real notification service backed by a throwing email queue — proves the wrap-and-log guarantee.
        var notifications = new OrderNotificationService(
            db, new ThrowingEmailQueueService(), new OwnerNotificationOptions(),
            NullLogger<OrderNotificationService>.Instance);
        var service = new RefundService(
            db, new FixedTimeProvider(Now), new FakeStripeClient(), notifications,
            NullLogger<RefundService>.Instance);

        var result = await service.RefundAsync(Req(amount: null));

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Refunded, db.Payments.Single().Status);
        Assert.Single(db.Refunds);
    }
}
