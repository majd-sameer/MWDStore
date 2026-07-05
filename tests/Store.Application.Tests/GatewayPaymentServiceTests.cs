using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Application.Payments.Stripe;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// The slice of <see cref="GatewayPaymentService"/> relevant to order-paid notifications: an approved
/// callback (or a settled Stripe session) advances the order to <c>PaymentReceived</c> and fires
/// <see cref="IOrderNotificationService.NotifyOrderPaidAsync"/> exactly once; a declined/unpaid outcome
/// does not notify at all.
/// </summary>
public class GatewayPaymentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Stripe client is never exercised by these tests (no Stripe keys configured).</summary>
    private sealed class UnusedStripeClient : IStripeClient
    {
        public Task<StripeSession> CreateCheckoutSessionAsync(StripeCheckoutRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StripeSession> GetCheckoutSessionAsync(string sessionId, string secretKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public StripeSession? ReadCheckoutSessionFromWebhook(string payload, string signatureHeader, string webhookSecret) =>
            throw new NotSupportedException();

        public Task<StripeRefund> CreateRefundAsync(StripeRefundRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static (StoreDbContext Db, FakeOrderNotificationService Notifications, GatewayPaymentService Service) NewService()
    {
        var db = TestDb.New();
        db.PaymentProviders.Add(new PaymentProvider { Id = "CoD", Name = "Cash on Delivery", IsEnabled = true });
        db.Users.Add(new User { Id = 1, UserName = "buyer@example.com", Email = "buyer@example.com", FullName = "Buyer" });
        db.Orders.Add(new Order
        {
            Id = 1,
            CustomerId = 1,
            OrderStatus = OrderStatus.PendingPayment,
            TrackingNumber = "T-1",
            OrderTotal = 42m
        });
        db.Payments.Add(new Payment
        {
            Id = 1,
            OrderId = 1,
            Amount = 42m,
            PaymentMethod = "CoD",
            Status = PaymentStatus.PendingExecution,
            CreatedOn = Now,
            LatestUpdatedOn = Now
        });
        db.SaveChanges();

        var notifications = new FakeOrderNotificationService();
        var service = new GatewayPaymentService(
            db, new FixedTimeProvider(Now), new UnusedStripeClient(), new PaymentsOptions(), notifications,
            NullLogger<GatewayPaymentService>.Instance);
        return (db, notifications, service);
    }

    [Fact]
    public async Task HandleCallbackAsync_Approved_AdvancesToPaymentReceived_AndNotifiesPaid()
    {
        var (db, notifications, service) = NewService();
        var callback = new GatewayCallback(1, "CoD", "APPROVED", "gw-tx-1", null);

        var result = await service.HandleCallbackAsync(callback);

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.PaymentReceived, db.Orders.Single().OrderStatus);
        var call = Assert.Single(notifications.Calls);
        Assert.Equal("Paid", call.Event);
        Assert.Equal(1, call.OrderId);
    }

    [Fact]
    public async Task HandleCallbackAsync_Declined_DoesNotNotify()
    {
        var (db, notifications, service) = NewService();
        var callback = new GatewayCallback(1, "CoD", "DECLINED", "gw-tx-1", null);

        var result = await service.HandleCallbackAsync(callback);

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.PaymentFailed, db.Orders.Single().OrderStatus);
        Assert.Empty(notifications.Calls);
    }
}
