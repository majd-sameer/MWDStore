using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Messaging;
using Store.Application.Orders;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Order-lifecycle email notifications: the right template/tokens/recipient for each event, the customer +
/// owner "copy" pairing, guest-vs-signed-in email resolution, the no-email skip, and that a broken email
/// queue never bubbles up to the caller (orders must place/pay/ship/cancel regardless of email health).
/// </summary>
public class OrderNotificationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
    private const string OwnerEmail = "owner@mystore.local";

    /// <summary>Fake <see cref="IEmailQueueService"/> that records enqueue calls, or throws on demand.</summary>
    private sealed class FakeEmailQueueService : IEmailQueueService
    {
        public List<(string Template, IReadOnlyDictionary<string, string?> Tokens, string To, string? ToName)> Enqueued { get; } = [];
        public Exception? ThrowOnEnqueue { get; set; }

        public Task<long> EnqueueAsync(
            string templateName, IReadOnlyDictionary<string, string?> tokens, string to, string? toName = null,
            long? emailAccountId = null, int priority = 0, CancellationToken cancellationToken = default)
        {
            if (ThrowOnEnqueue != null)
            {
                throw ThrowOnEnqueue;
            }

            Enqueued.Add((templateName, tokens, to, toName));
            return Task.FromResult((long)Enqueued.Count);
        }

        public Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private static OrderNotificationService NewService(
        StoreDbContext db, FakeEmailQueueService queue, string? ownerEmail = OwnerEmail) =>
        new(db, queue, new OwnerNotificationOptions { Email = ownerEmail! }, NullLogger<OrderNotificationService>.Instance);

    private static Order NewOrder(
        long id = 1, long customerId = 1, string? guestEmail = null, int status = OrderStatus.New,
        string? trackingNumber = "T-100", decimal total = 55.5m) => new()
    {
        Id = id,
        CustomerId = customerId,
        GuestEmail = guestEmail,
        OrderStatus = status,
        TrackingNumber = trackingNumber,
        OrderTotal = total
    };

    // ---- customer resolution -----------------------------------------------------------------

    [Fact]
    public async Task NotifyOrderPlacedAsync_GuestOrder_UsesGuestEmail_AndSkipsDbLookup()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(guestEmail: "shopper@example.com");
        order.ShippingAddress = new OrderAddress { ContactName = "Shopper Sam" };

        await NewService(db, queue).NotifyOrderPlacedAsync(order);

        var customerCopy = queue.Enqueued.Single(e => e.Template == "Order.Placed");
        Assert.Equal("shopper@example.com", customerCopy.To);
        Assert.Equal("Shopper Sam", customerCopy.ToName);
    }

    [Fact]
    public async Task NotifyOrderPlacedAsync_SignedInOrder_ResolvesEmailFromCustomerAccount()
    {
        using var db = TestDb.New();
        db.Users.Add(new User { Id = 1, UserName = "sam@example.com", Email = "sam@example.com", FullName = "Sam Signed-In" });
        db.SaveChanges();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(customerId: 1, guestEmail: null);

        await NewService(db, queue).NotifyOrderPlacedAsync(order);

        var customerCopy = queue.Enqueued.Single(e => e.Template == "Order.Placed");
        Assert.Equal("sam@example.com", customerCopy.To);
        Assert.Equal("Sam Signed-In", customerCopy.ToName);
        Assert.Equal("Sam Signed-In", customerCopy.Tokens["Customer.Name"]);
    }

    [Fact]
    public async Task NotifyOrderPlacedAsync_NoEmailOnFile_SkipsCustomerCopy_ButStillNotifiesOwner()
    {
        using var db = TestDb.New();
        // No matching User row and no GuestEmail: customer email cannot be resolved.
        var queue = new FakeEmailQueueService();
        var order = NewOrder(customerId: 999, guestEmail: null);

        await NewService(db, queue).NotifyOrderPlacedAsync(order);

        Assert.DoesNotContain(queue.Enqueued, e => e.Template == "Order.Placed");
        var ownerCopy = Assert.Single(queue.Enqueued);
        Assert.Equal("Order.Placed.OwnerCopy", ownerCopy.Template);
        Assert.Equal(OwnerEmail, ownerCopy.To);
    }

    // ---- tokens + recipients per event -------------------------------------------------------

    [Fact]
    public async Task NotifyOrderPlacedAsync_EnqueuesCustomerAndOwnerCopy_WithExpectedTokens()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(guestEmail: "buyer@example.com", trackingNumber: "T-777", total: 120m, status: OrderStatus.New);

        await NewService(db, queue).NotifyOrderPlacedAsync(order);

        Assert.Equal(2, queue.Enqueued.Count);
        var customerCopy = queue.Enqueued.Single(e => e.Template == "Order.Placed");
        var ownerCopy = queue.Enqueued.Single(e => e.Template == "Order.Placed.OwnerCopy");

        Assert.Equal("buyer@example.com", customerCopy.To);
        Assert.Equal(OwnerEmail, ownerCopy.To);
        Assert.Null(ownerCopy.ToName);

        Assert.Equal("T-777", customerCopy.Tokens["Order.Number"]);
        Assert.Equal("120.00", customerCopy.Tokens["Order.Total"]);
        Assert.Equal("New", customerCopy.Tokens["Order.Status"]);
        Assert.Equal("T-777", customerCopy.Tokens["Order.TrackingCode"]);
        // Owner copy shares the same rendered token set.
        Assert.Equal(customerCopy.Tokens, ownerCopy.Tokens);
    }

    [Fact]
    public async Task NotifyOrderPaidAsync_UsesOrderPaidTemplate()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(guestEmail: "buyer@example.com", status: OrderStatus.PaymentReceived);

        await NewService(db, queue).NotifyOrderPaidAsync(order);

        Assert.Contains(queue.Enqueued, e => e.Template == "Order.Paid" && e.To == "buyer@example.com");
        Assert.Contains(queue.Enqueued, e => e.Template == "Order.Paid.OwnerCopy" && e.To == OwnerEmail);
    }

    [Fact]
    public async Task NotifyOrderShippedAsync_UsesOrderShippedTemplate_WithTrackingCode()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(guestEmail: "buyer@example.com", status: OrderStatus.Shipped, trackingNumber: "555123");

        await NewService(db, queue).NotifyOrderShippedAsync(order);

        var customerCopy = queue.Enqueued.Single(e => e.Template == "Order.Shipped");
        Assert.Equal("555123", customerCopy.Tokens["Order.TrackingCode"]);
        Assert.Contains(queue.Enqueued, e => e.Template == "Order.Shipped.OwnerCopy");
    }

    [Fact]
    public async Task NotifyOrderCancelledAsync_UsesOrderCancelledTemplate()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(guestEmail: "buyer@example.com", status: OrderStatus.Canceled);

        await NewService(db, queue).NotifyOrderCancelledAsync(order);

        Assert.Contains(queue.Enqueued, e => e.Template == "Order.Cancelled" && e.To == "buyer@example.com");
        Assert.Contains(queue.Enqueued, e => e.Template == "Order.Cancelled.OwnerCopy" && e.To == OwnerEmail);
    }

    // ---- owner email missing -------------------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_NoOwnerEmailConfigured_SkipsOwnerCopy_ButStillNotifiesCustomer()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService();
        var order = NewOrder(guestEmail: "buyer@example.com");

        await NewService(db, queue, ownerEmail: "").NotifyOrderPlacedAsync(order);

        var only = Assert.Single(queue.Enqueued);
        Assert.Equal("Order.Placed", only.Template);
    }

    // ---- failure swallowing --------------------------------------------------------------------

    [Fact]
    public async Task NotifyOrderPlacedAsync_EnqueueThrows_DoesNotPropagate()
    {
        using var db = TestDb.New();
        var queue = new FakeEmailQueueService { ThrowOnEnqueue = new InvalidOperationException("template missing") };
        var order = NewOrder(guestEmail: "buyer@example.com");

        // Must complete without throwing even though every enqueue attempt fails.
        await NewService(db, queue).NotifyOrderPlacedAsync(order);

        Assert.Empty(queue.Enqueued);
    }
}
