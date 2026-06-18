namespace Store.Application.Inventory;

/// <summary>
/// Raised when a product's stock crosses from &lt;= 0 to &gt; 0. Stands in for SimplCommerce's MediatR
/// <c>ProductBackInStock</c> event so back-in-stock subscriptions can be notified.
/// </summary>
public interface IProductBackInStockNotifier
{
    Task NotifyAsync(long productId, CancellationToken cancellationToken = default);
}

/// <summary>Default no-op notifier (no subscription delivery wired up yet).</summary>
public sealed class NullProductBackInStockNotifier : IProductBackInStockNotifier
{
    public Task NotifyAsync(long productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
