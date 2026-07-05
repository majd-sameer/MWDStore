using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Inventory;
using Store.Application.Messaging;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

/// <summary>
/// <see cref="EmailProductBackInStockNotifier"/>: enqueues the <c>Product.BackInStock</c> template to every
/// subscriber of the product, clears the subscriptions once processed, ignores subscriptions for other
/// products, and never throws even when the email queue fails.
/// </summary>
public class EmailProductBackInStockNotifierTests
{
    /// <summary>Fake <see cref="IEmailQueueService"/> that records enqueue calls, or throws on demand.</summary>
    private sealed class FakeEmailQueueService : IEmailQueueService
    {
        public List<(string Template, IReadOnlyDictionary<string, string?> Tokens, string To)> Enqueued { get; } = [];
        public HashSet<string> ThrowFor { get; } = [];

        public Task<long> EnqueueAsync(
            string templateName, IReadOnlyDictionary<string, string?> tokens, string to, string? toName = null,
            long? emailAccountId = null, int priority = 0, CancellationToken cancellationToken = default)
        {
            if (ThrowFor.Contains(to))
            {
                throw new InvalidOperationException($"smtp rejected {to}");
            }

            Enqueued.Add((templateName, tokens, to));
            return Task.FromResult((long)Enqueued.Count);
        }

        public Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private static StoreDbContext Seed(long productId = 1, string productName = "Widget", params string[] subscriberEmails)
    {
        var db = TestDb.New();
        db.Products.Add(NewProduct(productId, productName, 10m));
        foreach (var email in subscriberEmails)
        {
            db.ProductBackInStockSubscriptions.Add(new ProductBackInStockSubscription
            {
                ProductId = productId,
                CustomerEmail = email
            });
        }

        db.SaveChanges();
        return db;
    }

    private static EmailProductBackInStockNotifier NewNotifier(StoreDbContext db, FakeEmailQueueService queue) =>
        new(db, queue, NullLogger<EmailProductBackInStockNotifier>.Instance);

    [Fact]
    public async Task NotifyAsync_EnqueuesOneEmailPerSubscriber_WithProductNameToken()
    {
        using var db = Seed(1, "Widget", "a@example.com", "b@example.com");
        var queue = new FakeEmailQueueService();
        var notifier = NewNotifier(db, queue);

        await notifier.NotifyAsync(1);

        Assert.Equal(2, queue.Enqueued.Count);
        Assert.All(queue.Enqueued, e => Assert.Equal("Product.BackInStock", e.Template));
        Assert.All(queue.Enqueued, e => Assert.Equal("Widget", e.Tokens["Product.Name"]));
        Assert.Contains(queue.Enqueued, e => e.To == "a@example.com");
        Assert.Contains(queue.Enqueued, e => e.To == "b@example.com");
    }

    [Fact]
    public async Task NotifyAsync_ClearsSubscriptions_ForThatProduct()
    {
        using var db = Seed(1, "Widget", "a@example.com", "b@example.com");
        var notifier = NewNotifier(db, new FakeEmailQueueService());

        await notifier.NotifyAsync(1);

        Assert.Empty(db.ProductBackInStockSubscriptions);
    }

    [Fact]
    public async Task NotifyAsync_IgnoresSubscriptionsForOtherProducts()
    {
        using var db = Seed(1, "Widget", "a@example.com");
        db.Products.Add(NewProduct(2, "Gadget", 5m));
        db.ProductBackInStockSubscriptions.Add(new ProductBackInStockSubscription { ProductId = 2, CustomerEmail = "c@example.com" });
        db.SaveChanges();
        var queue = new FakeEmailQueueService();
        var notifier = NewNotifier(db, queue);

        await notifier.NotifyAsync(1);

        var sent = Assert.Single(queue.Enqueued);
        Assert.Equal("a@example.com", sent.To);
        Assert.Single(db.ProductBackInStockSubscriptions); // product 2's subscription untouched
        Assert.Equal(2, db.ProductBackInStockSubscriptions.Single().ProductId);
    }

    [Fact]
    public async Task NotifyAsync_NoSubscribers_DoesNothing_AndDoesNotThrow()
    {
        using var db = Seed(1, "Widget");
        var queue = new FakeEmailQueueService();
        var notifier = NewNotifier(db, queue);

        await notifier.NotifyAsync(1);

        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task NotifyAsync_EnqueueFailure_IsSwallowed_OtherSubscribersStillNotified_AndSubscriptionsCleared()
    {
        using var db = Seed(1, "Widget", "a@example.com", "b@example.com");
        var queue = new FakeEmailQueueService();
        queue.ThrowFor.Add("a@example.com");
        var notifier = NewNotifier(db, queue);

        // Must not throw despite one subscriber's enqueue failing.
        await notifier.NotifyAsync(1);

        var sent = Assert.Single(queue.Enqueued);
        Assert.Equal("b@example.com", sent.To);
        Assert.Empty(db.ProductBackInStockSubscriptions); // still cleared, one-shot semantics
    }
}
