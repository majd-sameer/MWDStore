using Store.Application.Catalog.Pricing;
using Store.Application.Orders;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.ShoppingCart;
using Store.Application.Tax;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

/// <summary>
/// Stock is a ceiling on the bag, not just a checkout-time complaint.
///
/// <para>
/// Two rules are pinned here. First, adding a product the bag already holds raises that one line and
/// never pushes it past what is in stock. Second — and this is what makes an unpaid order a real
/// reservation — <c>Product.StockQuantity</c> is already net of every order holding units, so the
/// moment one shopper places an order those units leave everyone else's reach, and they only come
/// back when the payment window closes and the order is canceled.
/// </para>
/// </summary>
public class CartStockLimitTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private const long OtherCustomerId = 2;
    private const string Standard = "Standard";

    private static CartService NewCartService(StoreDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        return new CartService(db, new ProductPricingService(time), new CouponService(db, time), time,
            new Store.Application.Common.LocalMediaUrlBuilder());
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

    private static OrderAddressInfo Address() =>
        new() { CountryId = "JO", StateOrProvinceId = 1, ContactName = "Buyer" };

    // ---- 1. adding the same item raises the line, and stock caps it ---------

    [Fact]
    public async Task AddingTheSameProductRaisesTheOneLineUpToStock()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5));
        db.SaveChanges();
        var service = NewCartService(db);

        Assert.True((await service.AddToCartAsync(CustomerId, 1, 3)).Success);
        var second = await service.AddToCartAsync(CustomerId, 1, 3);

        // 3 + 3 would be 6 against 5 in stock: one line, capped, and the caller is told.
        Assert.True(second.Success);
        Assert.True(second.WasCapped);
        Assert.Equal(3, second.RequestedQuantity);
        Assert.Equal(5, second.Quantity);
        Assert.Equal(5, second.AvailableQuantity);

        var line = Assert.Single(db.Set<CartItem>().Where(x => x.CustomerId == CustomerId));
        Assert.Equal(5, line.Quantity);
    }

    [Fact]
    public async Task AddingIsRefusedOnceTheBagHoldsEveryUnitInStock()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 2));
        db.SaveChanges();
        var service = NewCartService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);

        var result = await service.AddToCartAsync(CustomerId, 1, 1);

        Assert.False(result.Success);
        Assert.Equal("out-of-stock", result.ErrorCode);
        Assert.Equal(2, result.AvailableQuantity);
        Assert.Equal(2, db.Set<CartItem>().Single().Quantity);
    }

    [Fact]
    public async Task AnUntrackedProductIsNotCapped()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Untracked", 10m));
        db.SaveChanges();
        var service = NewCartService(db);

        await service.AddToCartAsync(CustomerId, 1, 40);
        var result = await service.AddToCartAsync(CustomerId, 1, 60);

        Assert.True(result.Success);
        Assert.False(result.WasCapped);
        Assert.Null(result.AvailableQuantity);
        Assert.Equal(100, db.Set<CartItem>().Single().Quantity);
    }

    [Fact]
    public async Task AddingRefusesAWithdrawnOrUnknownProduct()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Withdrawn", 10m, published: false));
        db.SaveChanges();
        var service = NewCartService(db);

        Assert.Equal("unavailable", (await service.AddToCartAsync(CustomerId, 1, 1)).ErrorCode);
        // Previously a bogus id was only caught by the foreign key at SaveChanges.
        Assert.Equal("product-not-found", (await service.AddToCartAsync(CustomerId, 99, 1)).ErrorCode);
        Assert.Empty(db.Set<CartItem>());
    }

    [Fact]
    public async Task SettingAQuantityIsCappedAtStockRatherThanRefused()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 4));
        db.SaveChanges();
        var service = NewCartService(db);
        await service.AddToCartAsync(CustomerId, 1, 1);
        var itemId = db.Set<CartItem>().Single().Id;

        var result = await service.UpdateQuantityAsync(CustomerId, itemId, 9);

        Assert.True(result.Success);
        Assert.True(result.WasCapped);
        Assert.Equal(4, result.Quantity);
        Assert.Equal(4, db.Set<CartItem>().Single().Quantity);
    }

    [Fact]
    public async Task SettingAQuantityFailsWhenTheProductSoldOutMeanwhile()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 4);
        db.Products.Add(product);
        db.SaveChanges();
        var service = NewCartService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);
        var itemId = db.Set<CartItem>().Single().Id;

        product.StockQuantity = 0;
        await db.SaveChangesAsync();

        var result = await service.UpdateQuantityAsync(CustomerId, itemId, 1);

        Assert.False(result.Success);
        Assert.Equal("out-of-stock", result.ErrorCode);
    }

    // ---- 2. an unpaid order holds the stock against everyone else ----------

    [Fact]
    public async Task AnOrderAwaitingPaymentKeepsItsUnitsOutOfEveryoneElsesBag()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        db.SaveChanges();

        // One shopper checks out 4 of the 5 and is sent to the gateway — the order holds the stock
        // from the moment it is placed, before a single dinar is taken.
        var checkoutId = AddCheckout(db, [(product, 4)]);
        var order = (await NewOrderService(db).CreateOrderAsync(
            checkoutId, "MadfoatCom", 0m, Standard, Address(), Address())).Value!;
        order.OrderStatus = OrderStatus.PendingPayment;
        await db.SaveChangesAsync();

        var service = NewCartService(db);
        var capped = await service.AddToCartAsync(OtherCustomerId, 1, 3);

        // Only the 1 left over is reachable — the other shopper's unpaid order is holding the rest.
        Assert.True(capped.Success);
        Assert.True(capped.WasCapped);
        Assert.Equal(1, capped.Quantity);
        Assert.Equal(1, capped.AvailableQuantity);

        Assert.Equal("out-of-stock", (await service.AddToCartAsync(OtherCustomerId, 1, 1)).ErrorCode);
    }

    [Fact]
    public async Task TheHoldIsReleasedWhenThePaymentWindowClosesAndTheOrderIsCanceled()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        db.SaveChanges();

        var checkoutId = AddCheckout(db, [(product, 5)]);
        var orderService = NewOrderService(db);
        var order = (await orderService.CreateOrderAsync(
            checkoutId, "MadfoatCom", 0m, Standard, Address(), Address())).Value!;
        order.OrderStatus = OrderStatus.PendingPayment;
        await db.SaveChangesAsync();

        var service = NewCartService(db);
        Assert.Equal("out-of-stock", (await service.AddToCartAsync(OtherCustomerId, 1, 1)).ErrorCode);

        // This is what the reconciliation sweep does to an attempt that ran out of time.
        await orderService.CancelOrderAsync(order);

        var result = await service.AddToCartAsync(OtherCustomerId, 1, 5);
        Assert.True(result.Success);
        Assert.False(result.WasCapped);
        Assert.Equal(5, result.Quantity);
    }

    [Fact]
    public async Task CancelingTwiceDoesNotMintStock()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        db.SaveChanges();

        var checkoutId = AddCheckout(db, [(product, 3)]);
        var orderService = NewOrderService(db);
        var order = (await orderService.CreateOrderAsync(
            checkoutId, "MadfoatCom", 0m, Standard, Address(), Address())).Value!;
        Assert.Equal(2, db.Products.Single(p => p.Id == 1).StockQuantity);

        await orderService.CancelOrderAsync(order);
        await orderService.CancelOrderAsync(order);

        // A timeout sweep racing an admin cancel must not turn 5 units into 8.
        Assert.Equal(5, db.Products.Single(p => p.Id == 1).StockQuantity);
    }
}
