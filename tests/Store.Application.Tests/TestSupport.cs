using Microsoft.EntityFrameworkCore;
using Store.Application.Localization;
using Store.Application.Orders;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>Fixed-language fakes for <see cref="IRequestCulture"/>, replacing the old
/// Accept-Language-header-on-DefaultHttpContext / cultureId: "en-US" plumbing in tests.</summary>
public static class TestCulture
{
    public static readonly IRequestCulture Arabic = new RequestCultureContext { Language = ContentLanguage.Arabic };
    public static readonly IRequestCulture English = new RequestCultureContext { Language = ContentLanguage.English };
}

/// <summary>A <see cref="TimeProvider"/> whose "now" is frozen, so the time-boxed special-price
/// window in the pricing service is deterministic.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>
/// Fake <see cref="IOrderNotificationService"/> that records which event fired for which order instead of
/// touching the email queue. Used by tests that only care that <c>OrderService</c>/<c>GatewayPaymentService</c>
/// invoke the right notification at the right time — the notification service's own enqueue/token/failure
/// behavior is covered separately by <c>OrderNotificationServiceTests</c>.
/// </summary>
public sealed class FakeOrderNotificationService : IOrderNotificationService
{
    public List<(string Event, long OrderId)> Calls { get; } = [];

    public Task NotifyOrderPlacedAsync(Order order, CancellationToken cancellationToken = default) =>
        Record("Placed", order);

    public Task NotifyOrderPaidAsync(Order order, CancellationToken cancellationToken = default) =>
        Record("Paid", order);

    public Task NotifyOrderShippedAsync(Order order, CancellationToken cancellationToken = default) =>
        Record("Shipped", order);

    public Task NotifyOrderCancelledAsync(Order order, CancellationToken cancellationToken = default) =>
        Record("Cancelled", order);

    private Task Record(string eventName, Order order)
    {
        Calls.Add((eventName, order.Id));
        return Task.CompletedTask;
    }
}

internal static class TestDb
{
    /// <summary>A fresh isolated in-memory <see cref="StoreDbContext"/>.</summary>
    public static StoreDbContext New()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase("catalog-" + Guid.NewGuid())
            .EnableSensitiveDataLogging()
            .Options;
        return new StoreDbContext(options);
    }
}
