using Microsoft.EntityFrameworkCore;
using Store.Data;

namespace Store.Application.Tests;


public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

internal static class TestDb
{
    public static StoreDbContext New()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase("catalog-" + Guid.NewGuid())
            .EnableSensitiveDataLogging()
            .Options;
        return new StoreDbContext(options);
    }
}
