using Store.Application.Catalog.Pricing;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.ShoppingCart;
using Store.Application.Tax;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

/// <summary>
/// "Pay again" after a failed payment: pay the same order when everything is still orderable,
/// otherwise the whole order goes back to the cart with the missing lines flagged — and the cart
/// leaves those lines out of its totals.
/// </summary>
public class OrderRetryPaymentTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private const string Standard = "Standard";

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

    private static CartService NewCartService(StoreDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        return new CartService(db, new ProductPricingService(time), new CouponService(db, time), time,
            new Store.Application.Common.LocalMediaUrlBuilder());
    }

    private static OrderAddressInfo Address() =>
        new() { CountryId = "JO", StateOrProvinceId = 1, ContactName = "Buyer" };

    /// <summary>An order whose payment failed, holding the stock it took at checkout.</summary>
    private static async Task<Order> PlaceFailedOrderAsync(
        StoreDbContext db, IEnumerable<(Product Product, int Quantity)> lines)
    {
        var checkoutId = AddCheckout(db, lines);
        var order = (await NewOrderService(db).CreateOrderAsync(
            checkoutId, "MadfoatCom", 0m, Standard, Address(), Address())).Value!;

        order.OrderStatus = OrderStatus.PaymentFailed;
        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Amount = order.OrderTotal,
            PaymentMethod = "MadfoatCom",
            Status = PaymentStatus.Failed,
            FailureMessage = "The security code (CVV) does not match.",
            CreatedOn = Now,
            LatestUpdatedOn = Now
        });
        await db.SaveChangesAsync();

        return order;
    }

    [Fact]
    public async Task Retry_ClearsThePaymentWhenEverythingIsStillOrderable()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        var order = await PlaceFailedOrderAsync(db, [(product, 3)]);

        var result = await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId);

        Assert.True(result.Success);
        Assert.True(result.Value!.CanPay);
        Assert.False(result.Value.MovedToCart);
        Assert.Empty(result.Value.UnavailableItems);

        // Nothing moved: the order still holds its stock and is still payable.
        Assert.Equal(OrderStatus.PaymentFailed, order.OrderStatus);
        Assert.Empty(db.Set<CartItem>());
        Assert.Equal(2, db.Products.Single(p => p.Id == 1).StockQuantity);
    }

    [Fact]
    public async Task Retry_MovesEverythingToTheCartWhenAProductIsWithdrawn()
    {
        using var db = TestDb.New();
        var kept = NewProduct(1, "Kept", 10m, stockTracking: true, stock: 5);
        var withdrawn = NewProduct(2, "Withdrawn", 20m);
        db.Products.AddRange(kept, withdrawn);
        var order = await PlaceFailedOrderAsync(db, [(kept, 3), (withdrawn, 1)]);

        withdrawn.IsPublished = false;
        await db.SaveChangesAsync();

        var result = await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId);

        Assert.True(result.Success);
        Assert.False(result.Value!.CanPay);
        Assert.True(result.Value.MovedToCart);

        var missing = Assert.Single(result.Value.UnavailableItems);
        Assert.Equal(2, missing.ProductId);
        Assert.Equal("unavailable", missing.Reason);

        // Every line is in the cart — the unavailable one included, for the shopper to see.
        var cart = db.Set<CartItem>().Where(c => c.CustomerId == CustomerId).OrderBy(c => c.ProductId).ToList();
        Assert.Equal(2, cart.Count);
        Assert.Equal(3, cart[0].Quantity);
        Assert.Equal(1, cart[1].Quantity);

        // The order is canceled and its stock released, so the cart isn't fighting it for inventory.
        Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
        Assert.Equal(5, db.Products.Single(p => p.Id == 1).StockQuantity);
    }

    [Fact]
    public async Task Retry_ReportsWhatIsLeftWhenStockRanOutOnACanceledOrder()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(product);
        var order = await PlaceFailedOrderAsync(db, [(product, 3)]);

        // The payment timeout canceled the order and put the stock back; someone else then bought it.
        await NewOrderService(db).CancelOrderAsync(order);
        product.StockQuantity = 1;
        await db.SaveChangesAsync();

        var result = await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId);

        Assert.True(result.Success);
        Assert.True(result.Value!.MovedToCart);

        var missing = Assert.Single(result.Value.UnavailableItems);
        Assert.Equal("out-of-stock", missing.Reason);
        Assert.Equal(3, missing.RequestedQuantity);
        Assert.Equal(1, missing.AvailableQuantity);
    }

    [Fact]
    public async Task Retry_RaisesAnExistingCartLineRatherThanDoublingIt()
    {
        using var db = TestDb.New();
        var kept = NewProduct(1, "Kept", 10m);
        var withdrawn = NewProduct(2, "Withdrawn", 20m);
        db.Products.AddRange(kept, withdrawn);
        var order = await PlaceFailedOrderAsync(db, [(kept, 3), (withdrawn, 1)]);
        await NewCartService(db).AddToCartAsync(CustomerId, 1, 1);

        withdrawn.IsPublished = false;
        await db.SaveChangesAsync();

        // Twice, as an impatient double-click would.
        await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId);
        await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId);

        var line = db.Set<CartItem>().Single(c => c.ProductId == 1);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task Retry_RefusesAnOrderThatIsAlreadyPaid()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m);
        db.Products.Add(product);
        var order = await PlaceFailedOrderAsync(db, [(product, 1)]);
        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Amount = order.OrderTotal,
            PaymentMethod = "MadfoatCom",
            Status = PaymentStatus.Succeeded,
            CreatedOn = Now,
            LatestUpdatedOn = Now
        });
        await db.SaveChangesAsync();

        var result = await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId);

        Assert.False(result.Success);
        Assert.Contains("already been paid", result.Error);
    }

    [Fact]
    public async Task Retry_RefusesSomeoneElsesOrder()
    {
        using var db = TestDb.New();
        var product = NewProduct(1, "Tracked", 10m);
        db.Products.Add(product);
        var order = await PlaceFailedOrderAsync(db, [(product, 1)]);

        var result = await NewOrderService(db).RetryPaymentAsync(order.Id, CustomerId + 99);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    // ---- the cart the shopper lands on -----------------------------------

    /// <summary>
    /// A line goes bad <i>after</i> it is in the bag — the add path itself refuses a withdrawn product
    /// and caps a short one, so this is the only way an unbuyable line can exist: the shopper put it
    /// there while it was fine and the world moved on (someone else bought the stock, staff pulled the
    /// product). Such lines stay visible but must not be priced.
    /// </summary>
    [Fact]
    public async Task Cart_ShowsUnavailableLinesButLeavesThemOutOfTheTotals()
    {
        using var db = TestDb.New();
        var kept = NewProduct(1, "Kept", 10m);
        var withdrawn = NewProduct(2, "Withdrawn", 20m);
        var short_ = NewProduct(3, "Short", 30m, stockTracking: true, stock: 2);
        db.Products.AddRange(kept, withdrawn, short_);
        await db.SaveChangesAsync();

        var cartService = NewCartService(db);
        await cartService.AddToCartAsync(CustomerId, 1, 2);
        await cartService.AddToCartAsync(CustomerId, 2, 1);
        await cartService.AddToCartAsync(CustomerId, 3, 2);

        // ...and then the world moves on.
        withdrawn.IsPublished = false;
        short_.StockQuantity = 1;
        await db.SaveChangesAsync();

        var cart = (await cartService.GetCartDetailsAsync(CustomerId))!;

        Assert.Equal(3, cart.Items.Count);
        Assert.True(cart.Items.Single(i => i.ProductId == 1).IsAvailable);
        Assert.False(cart.Items.Single(i => i.ProductId == 2).IsAvailable);
        Assert.False(cart.Items.Single(i => i.ProductId == 3).IsAvailable);
        Assert.Equal(1, cart.Items.Single(i => i.ProductId == 3).AvailableQuantity);

        // Only the buyable line is priced: 2 × 10.
        Assert.Equal(20m, cart.SubTotal);
    }
}
