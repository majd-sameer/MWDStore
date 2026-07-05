using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Messaging;
using Store.Data;

namespace Store.Application.Inventory;

/// <summary>
/// Default <see cref="IProductBackInStockNotifier"/>. Enqueues the <c>Product.BackInStock</c> template
/// (see <c>EmailSeeder</c>) to every <see cref="Store.Domain.ProductBackInStockSubscription"/> for the
/// product, then clears those subscriptions — the entity has no "notified" flag, so a one-shot
/// notify-then-delete is the only lifecycle it supports. Per-subscriber enqueue failures are logged and
/// swallowed; this method never throws, matching <see cref="StockService"/>'s expectation that a
/// back-in-stock notification can never break a stock update.
/// </summary>
public sealed class EmailProductBackInStockNotifier : IProductBackInStockNotifier
{
    private readonly StoreDbContext _db;
    private readonly IEmailQueueService _emailQueue;
    private readonly ILogger<EmailProductBackInStockNotifier> _logger;

    public EmailProductBackInStockNotifier(
        StoreDbContext db, IEmailQueueService emailQueue, ILogger<EmailProductBackInStockNotifier> logger)
    {
        _db = db;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task NotifyAsync(long productId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _db.ProductBackInStockSubscriptions
            .Where(s => s.ProductId == productId)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        var productName = await _db.Products
            .Where(p => p.Id == productId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Item";

        var tokens = new Dictionary<string, string?>
        {
            ["Product.Name"] = productName
        };

        foreach (var subscription in subscriptions)
        {
            if (string.IsNullOrWhiteSpace(subscription.CustomerEmail))
            {
                continue;
            }

            try
            {
                await _emailQueue.EnqueueAsync(
                    "Product.BackInStock", tokens, subscription.CustomerEmail,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex, "Failed to enqueue back-in-stock email for product {ProductId} to {Email}.",
                    productId, subscription.CustomerEmail);
            }
        }

        // One-shot: whether or not every enqueue succeeded, this batch of subscriptions has been processed.
        _db.ProductBackInStockSubscriptions.RemoveRange(subscriptions);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
