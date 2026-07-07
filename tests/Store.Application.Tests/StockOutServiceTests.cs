using Store.Application.Auditing;
using Store.Application.Catalog.Pricing;
using Store.Application.Inventory;
using Store.Application.Orders;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.Tax;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

/// <summary>
/// Stock-out workflow (Phase 3): channel-required-for-sale and over-stock validation, the
/// stock/product decrement + StockHistory write + audit entry, the performer override, and the
/// storefront order flow stamping Sale / OnlineStore on its stock-history rows.
/// </summary>
public class StockOutServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);
    private const long WarehouseId = 1;

    private sealed class CapturingAudit : IAuditService
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopNotifier : IProductBackInStockNotifier
    {
        public Task NotifyAsync(long productId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static StoreDbContext SeedStock(int productStock, int warehouseStock)
    {
        var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Widget", 10m, stockTracking: true, stock: productStock));
        db.Set<Stock>().Add(new Stock { Id = 1, ProductId = 1, WarehouseId = WarehouseId, Quantity = warehouseStock });
        db.SaveChanges();
        return db;
    }

    private static (StockService Service, CapturingAudit Audit) NewService(StoreDbContext db)
    {
        var audit = new CapturingAudit();
        return (new StockService(db, new NoopNotifier(), new FixedTimeProvider(Now), audit), audit);
    }

    private static AuditActor Keeper(long id = 7) => new(id, "keeper", AppRole.WarehouseKeeper, null, null);

    private static class AppRole
    {
        public const string WarehouseKeeper = "warehouse-keeper";
        public const string Admin = "admin";
    }

    [Fact]
    public async Task StockOut_sale_without_channel_is_rejected()
    {
        using var db = SeedStock(productStock: 5, warehouseStock: 5);
        var (service, audit) = NewService(db);

        var result = await service.StockOutAsync(
            new StockOutRequest { ProductId = 1, WarehouseId = WarehouseId, Quantity = 1, Reason = StockOutReason.Sale },
            Keeper());

        Assert.False(result.Success);
        Assert.Equal(5, db.Set<Stock>().Single().Quantity); // unchanged
        Assert.Empty(db.Set<StockHistory>());
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task StockOut_over_on_hand_is_rejected()
    {
        using var db = SeedStock(productStock: 5, warehouseStock: 3);
        var (service, _) = NewService(db);

        var result = await service.StockOutAsync(
            new StockOutRequest { ProductId = 1, WarehouseId = WarehouseId, Quantity = 4, Reason = StockOutReason.Gift },
            Keeper());

        Assert.False(result.Success);
        Assert.Equal(3, db.Set<Stock>().Single().Quantity); // unchanged
        Assert.Empty(db.Set<StockHistory>());
    }

    [Fact]
    public async Task StockOut_decrements_writes_history_and_audits()
    {
        using var db = SeedStock(productStock: 5, warehouseStock: 5);
        var (service, audit) = NewService(db);

        var result = await service.StockOutAsync(
            new StockOutRequest
            {
                ProductId = 1,
                WarehouseId = WarehouseId,
                Quantity = 2,
                Reason = StockOutReason.Gift,
                RecipientOrRef = "VIP client",
                Note = "handover",
            },
            Keeper(7));

        Assert.True(result.Success);
        Assert.Equal(3, db.Set<Stock>().Single().Quantity);
        Assert.Equal(3, db.Products.Single().StockQuantity);

        var history = Assert.Single(db.Set<StockHistory>());
        Assert.Equal(-2, history.AdjustedQuantity);
        Assert.Equal(StockOutReason.Gift, history.Reason);
        Assert.Null(history.Channel);
        Assert.Equal(7, history.PerformedById);
        Assert.Equal(7, history.CreatedById);
        Assert.Equal("VIP client", history.RecipientOrRef);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("StockOut", entry.Action);
        Assert.Equal("Inventory", entry.Area);
        Assert.Equal("Product", entry.EntityType);
        Assert.Equal(1, entry.EntityId);
    }

    [Fact]
    public async Task StockOut_honours_performer_override()
    {
        using var db = SeedStock(productStock: 5, warehouseStock: 5);
        var (service, _) = NewService(db);

        var result = await service.StockOutAsync(
            new StockOutRequest
            {
                ProductId = 1,
                WarehouseId = WarehouseId,
                Quantity = 1,
                Reason = StockOutReason.Sale,
                Channel = SalesChannel.Showroom,
                PerformedById = 99,
            },
            new AuditActor(7, "boss", AppRole.Admin, null, null));

        Assert.True(result.Success);
        var history = Assert.Single(db.Set<StockHistory>());
        Assert.Equal(99, history.PerformedById); // the overridden performer
        Assert.Equal(7, history.CreatedById);     // the recording admin
    }

    [Fact]
    public async Task Order_placement_stamps_sale_and_online_store()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        db.Set<Stock>().Add(new Stock { Id = 1, ProductId = 1, WarehouseId = WarehouseId, Quantity = 5 });
        db.SaveChanges();
        var checkoutId = AddCheckout(db, [(product, 2)]);

        var result = await CreateOrder(db, checkoutId);

        Assert.True(result.Success);
        var history = Assert.Single(db.Set<StockHistory>());
        Assert.Equal(StockOutReason.Sale, history.Reason);
        Assert.Equal(SalesChannel.OnlineStore, history.Channel);
        Assert.Null(history.PerformedById);
        Assert.Equal(-2, history.AdjustedQuantity);
        Assert.Equal(3, db.Set<Stock>().Single().Quantity);   // warehouse decremented
        Assert.Equal(3, db.Products.Single().StockQuantity);  // and the denormalized total
    }

    private static Task<Store.Application.Common.Result<Order>> CreateOrder(StoreDbContext db, Guid checkoutId)
    {
        var time = new FixedTimeProvider(Now);
        var shipping = new ConfiguredShippingPriceService(new ShippingOptions
        {
            Methods = [new ShippingMethodSetting { Name = "Standard", Price = 0m, MinOrderSubtotal = 0m }],
        });
        var service = new OrderService(
            db, new CouponService(db, time), new TaxService(db), shipping, new ProductPricingService(time), time);

        var address = new OrderAddressInfo { CountryId = "US", StateOrProvinceId = 1, ContactName = "Buyer" };
        return service.CreateOrderAsync(checkoutId, "card", 0m, "Standard", address, address);
    }
}
