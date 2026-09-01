using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Catalog.Pricing;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Application.Payments.PayTabs;
using Store.Application.Payments.Stripe;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.Tax;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

/// <summary>
/// MadfoatCom (PayTabs) settlement: the outcome always comes from the gateway's query API, is recorded
/// as its own row in <c>Payment</c>, and an attempt nobody ever completed is voided by the
/// reconciliation sweep rather than left pending forever.
/// </summary>
public class PayTabsSettlementTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private const string TranRef = "TST0000012345";
    private const string Standard = "Standard";

    // ---- harness ---------------------------------------------------------

    /// <summary>An <see cref="IPayTabsClient"/> that answers the query however the test needs.</summary>
    private sealed class FakePayTabsClient : IPayTabsClient
    {
        public string Status { get; set; } = PayTabsResponseStatus.Authorised;

        /// <summary>Set to make the query fail the way an unreachable gateway does.</summary>
        public bool Unreachable { get; set; }

        public int Queries { get; private set; }

        public Task<PayTabsPage> CreateHostedPageAsync(
            PayTabsPageRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Settlement never creates a page.");

        public Task<PayTabsTransaction> QueryTransactionAsync(
            string baseUrl, string profileId, string serverKey, string tranRef,
            CancellationToken cancellationToken = default)
        {
            Queries++;

            if (Unreachable)
            {
                throw new PayTabsException("Could not reach PayTabs: connection refused.");
            }

            return Task.FromResult(new PayTabsTransaction(tranRef, null, Status, "100", "Approved"));
        }
    }

    /// <summary>Stripe is a constructor dependency the PayTabs paths never touch.</summary>
    private sealed class UnusedStripeClient : IStripeClient
    {
        public Task<StripeSession> CreateCheckoutSessionAsync(
            StripeCheckoutRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StripeSession> GetCheckoutSessionAsync(
            string sessionId, string secretKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public StripeSession? ReadCheckoutSessionFromWebhook(
            string payload, string signatureHeader, string webhookSecret) => throw new NotSupportedException();
    }

    private static OrderService NewOrderService(StoreDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        var shipping = new ConfiguredShippingPriceService(new ShippingOptions
        {
            Methods = [new ShippingMethodSetting { Name = Standard, Price = 0m, MinOrderSubtotal = 0m }]
        });
        return new OrderService(
            db, new CouponService(db, time), new TaxService(db), shipping, new ProductPricingService(time), time);
    }

    private static GatewayPaymentService NewGateway(
        StoreDbContext db, IPayTabsClient payTabs, PaymentsOptions? options = null) =>
        new(db,
            new FixedTimeProvider(Now),
            new UnusedStripeClient(),
            payTabs,
            NewOrderService(db),
            options ?? new PaymentsOptions(),
            NullLogger<GatewayPaymentService>.Instance);

    /// <summary>
    /// A stock-tracked order that has been sent to the hosted page: the provider is configured, the
    /// order is PendingPayment, and one attempt row carries the tran_ref PayTabs issued.
    /// </summary>
    private static async Task<(Order Order, Payment Attempt)> SeedAttemptAsync(
        StoreDbContext db, DateTimeOffset attemptCreatedOn, string? tranRef = TranRef)
    {
        db.PaymentProviders.Add(new PaymentProvider
        {
            Id = "MadfoatCom",
            Name = "MadfoatCom",
            IsEnabled = true,
            AdditionalSettings = """{"profileId":"12345","serverKey":"SKEY","region":"JOR","currency":"jod"}"""
        });

        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        var checkoutId = AddCheckout(db, [(product, 3)]);

        var order = (await NewOrderService(db).CreateOrderAsync(
            checkoutId, "MadfoatCom", 0m, Standard,
            new OrderAddressInfo { CountryId = "JO", StateOrProvinceId = 1, ContactName = "Buyer" },
            new OrderAddressInfo { CountryId = "JO", StateOrProvinceId = 1, ContactName = "Buyer" })).Value!;

        order.OrderStatus = OrderStatus.PendingPayment;

        var attempt = new Payment
        {
            OrderId = order.Id,
            Amount = order.OrderTotal,
            PaymentMethod = "MadfoatCom",
            GatewayTransactionId = tranRef,
            Status = PaymentStatus.PendingExecution,
            CreatedOn = attemptCreatedOn,
            LatestUpdatedOn = attemptCreatedOn
        };
        db.Payments.Add(attempt);
        await db.SaveChangesAsync();

        return (order, attempt);
    }

    // ---- settlement from the return leg / IPN -----------------------------

    [Fact]
    public async Task Settle_ApprovedQuery_RecordsSettlementRowAndMarksOrderPaid()
    {
        using var db = TestDb.New();
        var (order, attempt) = await SeedAttemptAsync(db, Now.AddMinutes(-3));
        var payTabs = new FakePayTabsClient { Status = PayTabsResponseStatus.Authorised };

        var result = await NewGateway(db, payTabs).SettlePayTabsTransactionAsync(TranRef);

        Assert.True(result.Success);
        Assert.True(result.Value!.Approved);
        Assert.Equal(1, payTabs.Queries);

        // The attempt stays as the record of the redirect; the outcome is its own row.
        var rows = db.Payments.OrderBy(p => p.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(PaymentStatus.PendingExecution, rows[0].Status);
        Assert.Equal(attempt.Id, rows[0].Id);
        Assert.Equal(PaymentStatus.Succeeded, rows[1].Status);
        Assert.Equal(TranRef, rows[1].GatewayTransactionId);
        Assert.Equal(attempt.Amount, rows[1].Amount);

        Assert.Equal(OrderStatus.PaymentReceived, order.OrderStatus);
    }

    [Fact]
    public async Task Settle_DeclinedQuery_RecordsFailureAndLeavesOrderPayable()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-3));

        var result = await NewGateway(db, new FakePayTabsClient { Status = PayTabsResponseStatus.Declined })
            .SettlePayTabsTransactionAsync(TranRef);

        Assert.True(result.Success);
        Assert.False(result.Value!.Approved);
        Assert.Equal(PaymentStatus.Failed, db.Payments.OrderBy(p => p.Id).Last().Status);
        Assert.Equal(OrderStatus.PendingPayment, order.OrderStatus);
    }

    [Fact]
    public async Task Settle_PendingQuery_RecordsNothing()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-3));

        var result = await NewGateway(db, new FakePayTabsClient { Status = PayTabsResponseStatus.Pending })
            .SettlePayTabsTransactionAsync(TranRef);

        Assert.True(result.Success);
        Assert.False(result.Value!.Approved);
        Assert.Single(db.Payments);
        Assert.Equal(OrderStatus.PendingPayment, order.OrderStatus);
    }

    [Fact]
    public async Task Settle_IsIdempotentAcrossReturnAndCallback()
    {
        using var db = TestDb.New();
        await SeedAttemptAsync(db, Now.AddMinutes(-3));
        var payTabs = new FakePayTabsClient();
        var gateway = NewGateway(db, payTabs);

        await gateway.SettlePayTabsTransactionAsync(TranRef);
        var second = await gateway.SettlePayTabsTransactionAsync(TranRef);

        Assert.True(second.Value!.Approved);
        // The recorded success is the final word: no second settlement row, no second query.
        Assert.Equal(2, db.Payments.Count());
        Assert.Equal(1, payTabs.Queries);
    }

    // ---- reconciliation sweep --------------------------------------------

    [Fact]
    public async Task Reconcile_SettlesAPaymentTheShopperNeverCameBackFrom()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-5));

        var decided = await NewGateway(db, new FakePayTabsClient()).ReconcilePendingPayTabsPaymentsAsync();

        Assert.Equal(1, decided);
        Assert.Equal(PaymentStatus.Succeeded, db.Payments.OrderBy(p => p.Id).Last().Status);
        Assert.Equal(OrderStatus.PaymentReceived, order.OrderStatus);
    }

    [Fact]
    public async Task Reconcile_VoidsAndCancelsAfterTheTimeout()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-40));

        var decided = await NewGateway(db, new FakePayTabsClient { Status = PayTabsResponseStatus.Pending })
            .ReconcilePendingPayTabsPaymentsAsync();

        Assert.Equal(1, decided);

        var settlement = db.Payments.OrderBy(p => p.Id).Last();
        Assert.Equal(PaymentStatus.Voided, settlement.Status);
        Assert.Contains("30 minutes", settlement.FailureMessage);

        Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
        Assert.Equal(5, db.Products.Single(p => p.Id == 1).StockQuantity); // restocked
    }

    [Fact]
    public async Task Reconcile_LeavesAnAttemptAloneBeforeTheTimeout()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-5));

        var decided = await NewGateway(db, new FakePayTabsClient { Status = PayTabsResponseStatus.Pending })
            .ReconcilePendingPayTabsPaymentsAsync();

        Assert.Equal(0, decided);
        Assert.Single(db.Payments);
        Assert.Equal(OrderStatus.PendingPayment, order.OrderStatus);
        Assert.Equal(2, db.Products.Single(p => p.Id == 1).StockQuantity); // stock still committed
    }

    [Fact]
    public async Task Reconcile_SkipsAnAttemptStillInsideTheGracePeriod()
    {
        using var db = TestDb.New();
        await SeedAttemptAsync(db, Now.AddSeconds(-30));
        var payTabs = new FakePayTabsClient();

        var decided = await NewGateway(db, payTabs).ReconcilePendingPayTabsPaymentsAsync();

        // The shopper is still on the hosted page — their own return settles it.
        Assert.Equal(0, decided);
        Assert.Equal(0, payTabs.Queries);
    }

    [Fact]
    public async Task Reconcile_DoesNotVoidWhenTheGatewayCannotBeReached()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-40));

        var decided = await NewGateway(db, new FakePayTabsClient { Unreachable = true })
            .ReconcilePendingPayTabsPaymentsAsync();

        // Cancelling a shopper's order over a network blip would be worse than waiting for the next pass.
        Assert.Equal(0, decided);
        Assert.Single(db.Payments);
        Assert.Equal(OrderStatus.PendingPayment, order.OrderStatus);
    }

    [Fact]
    public async Task Reconcile_VoidsAnAttemptThatNeverReachedTheGateway()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-40), tranRef: null);
        var payTabs = new FakePayTabsClient();

        var decided = await NewGateway(db, payTabs).ReconcilePendingPayTabsPaymentsAsync();

        Assert.Equal(1, decided);
        Assert.Equal(0, payTabs.Queries); // nothing to ask about without a tran_ref
        Assert.Equal(PaymentStatus.Voided, db.Payments.OrderBy(p => p.Id).Last().Status);
        Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
    }

    [Fact]
    public async Task Reconcile_IgnoresAnAttemptThatIsAlreadySettled()
    {
        using var db = TestDb.New();
        await SeedAttemptAsync(db, Now.AddMinutes(-40));
        var payTabs = new FakePayTabsClient();
        var gateway = NewGateway(db, payTabs);

        await gateway.SettlePayTabsTransactionAsync(TranRef);
        var decided = await gateway.ReconcilePendingPayTabsPaymentsAsync();

        Assert.Equal(0, decided);
        Assert.Equal(2, db.Payments.Count());
        Assert.Equal(1, payTabs.Queries);
    }

    [Fact]
    public async Task Reconcile_DoesNotVoidAStaleAttemptWhileTheShopperIsRetrying()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-40));

        // The shopper abandoned the first page and started again a minute ago.
        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Amount = order.OrderTotal,
            PaymentMethod = "MadfoatCom",
            GatewayTransactionId = "TST0000099999",
            Status = PaymentStatus.PendingExecution,
            CreatedOn = Now.AddMinutes(-1),
            LatestUpdatedOn = Now.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var decided = await NewGateway(db, new FakePayTabsClient { Status = PayTabsResponseStatus.Pending })
            .ReconcilePendingPayTabsPaymentsAsync();

        // Voiding the stale first attempt would cancel an order that is being paid right now.
        Assert.Equal(0, decided);
        Assert.DoesNotContain(db.Payments, p => p.Status == PaymentStatus.Voided);
        Assert.Equal(OrderStatus.PendingPayment, order.OrderStatus);
    }

    [Fact]
    public async Task Reconcile_HonoursAConfiguredTimeout()
    {
        using var db = TestDb.New();
        var (order, _) = await SeedAttemptAsync(db, Now.AddMinutes(-10));
        var options = new PaymentsOptions { PendingPaymentTimeoutMinutes = 5, ReconciliationGraceMinutes = 1 };

        var decided = await NewGateway(db, new FakePayTabsClient { Status = PayTabsResponseStatus.Pending }, options)
            .ReconcilePendingPayTabsPaymentsAsync();

        Assert.Equal(1, decided);
        Assert.Equal(PaymentStatus.Voided, db.Payments.OrderBy(p => p.Id).Last().Status);
        Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
    }
}
