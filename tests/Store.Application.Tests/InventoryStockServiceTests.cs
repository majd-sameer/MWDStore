using Store.Application.Inventory;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;


public class InventoryStockServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private const long WarehouseId = 1;

    private sealed class CapturingNotifier : IProductBackInStockNotifier
    {
        public List<long> Notified { get; } = [];

        public Task NotifyAsync(long productId, CancellationToken cancellationToken = default)
        {
            Notified.Add(productId);
            return Task.CompletedTask;
        }
    }

    private static StoreDbContext Seed(long productId, int productStock, int warehouseStock)
    {
        var db = TestDb.New();
        db.Products.Add(NewProduct(productId, "Widget", 10m, stockTracking: true, stock: productStock));
        db.Set<Stock>().Add(new Stock { Id = productId, ProductId = productId, WarehouseId = WarehouseId, Quantity = warehouseStock });
        db.SaveChanges();
        return db;
    }

    private static (StockService Service, CapturingNotifier Notifier) NewService(StoreDbContext db)
    {
        var notifier = new CapturingNotifier();
        return (new StockService(db, notifier, new FixedTimeProvider(Now)), notifier);
    }

    private static StockUpdateRequest Request(long productId, int adjusted) => new()
    {
        ProductId = productId,
        WarehouseId = WarehouseId,
        AdjustedQuantity = adjusted,
        Note = "test",
        UserId = 1
    };

    [Fact]
    public async Task Increment_AddsToWarehouseAndProduct_AndWritesHistory()
    {
        using var db = Seed(productId: 1, productStock: 5, warehouseStock: 10);
        var (service, _) = NewService(db);

        await service.UpdateStockAsync(Request(1, +3));

        Assert.Equal(13, db.Set<Stock>().Single().Quantity);
        Assert.Equal(8, db.Products.Single().StockQuantity); // mirrored
        var history = Assert.Single(db.Set<StockHistory>());
        Assert.Equal(3, history.AdjustedQuantity);
        Assert.Equal(Now, history.CreatedOn);
    }

    [Fact]
    public async Task Decrement_ClampsWarehouseAtZero_AndRecordsRequestedAmount()
    {
        using var db = Seed(productId: 1, productStock: 2, warehouseStock: 2);
        var (service, _) = NewService(db);

        await service.UpdateStockAsync(Request(1, -5)); // only 2 available -> clamp to -2

        Assert.Equal(0, db.Set<Stock>().Single().Quantity);
        Assert.Equal(0, db.Products.Single().StockQuantity); // mirror also moved by the clamped -2
        Assert.Equal(-5, db.Set<StockHistory>().Single().AdjustedQuantity); // audit keeps the request
    }

    [Fact]
    public async Task BackInStock_FiresOnlyWhenCrossingFromZeroToPositive()
    {
        using var db = Seed(productId: 1, productStock: 0, warehouseStock: 0);
        var (service, notifier) = NewService(db);

        await service.UpdateStockAsync(Request(1, +5));

        Assert.Equal([1L], notifier.Notified);
    }

    [Fact]
    public async Task BackInStock_DoesNotFireWhenAlreadyPositive()
    {
        using var db = Seed(productId: 1, productStock: 3, warehouseStock: 3);
        var (service, notifier) = NewService(db);

        await service.UpdateStockAsync(Request(1, +2));

        Assert.Empty(notifier.Notified);
    }
}
