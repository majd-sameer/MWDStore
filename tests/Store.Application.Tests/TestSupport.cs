using Microsoft.EntityFrameworkCore;
using Store.Data;

namespace Store.Application.Tests;

/// <summary>A <see cref="TimeProvider"/> whose "now" is frozen, so the time-boxed special-price
/// window in the pricing service is deterministic.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
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
