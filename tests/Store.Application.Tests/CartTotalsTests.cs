using Store.Application.Catalog.Pricing;
using Store.Application.Pricing.Coupons;
using Store.Application.ShoppingCart;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

public class CartTotalsTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveStart = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveEnd = new(2025, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static CartService NewService(StoreDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        return new CartService(db, new ProductPricingService(time), new CouponService(db, time), time,
            new Store.Application.Common.LocalMediaUrlBuilder());
    }

    // ---- add / update / remove -------------------------------------------

    [Fact]
    public async Task AddToCart_CreatesThenIncrementsQuantity()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Widget", 10m));
        db.SaveChanges();

        var service = NewService(db);
        Assert.True((await service.AddToCartAsync(CustomerId, 1, 2)).Success);
        Assert.True((await service.AddToCartAsync(CustomerId, 1, 3)).Success);

        var item = Assert.Single(db.Set<CartItem>().Where(x => x.CustomerId == CustomerId));
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public async Task AddToCart_RejectsNonPositiveQuantity()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Widget", 10m));
        db.SaveChanges();

        var result = await NewService(db).AddToCartAsync(CustomerId, 1, 0);

        Assert.False(result.Success);
        Assert.Equal("wrong-quantity", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateQuantity_SetsValue_AndValidates()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Widget", 10m));
        db.SaveChanges();
        var service = NewService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);
        var itemId = db.Set<CartItem>().Single().Id;

        Assert.True(await service.UpdateQuantityAsync(CustomerId, itemId, 7));
        Assert.Equal(7, db.Set<CartItem>().Single().Quantity);

        Assert.False(await service.UpdateQuantityAsync(CustomerId, itemId, 0));   // invalid quantity
        Assert.False(await service.UpdateQuantityAsync(999, itemId, 3));          // wrong owner
    }

    [Fact]
    public async Task RemoveItem_DeletesOwnedLine()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Widget", 10m));
        db.SaveChanges();
        var service = NewService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);
        var itemId = db.Set<CartItem>().Single().Id;

        Assert.False(await service.RemoveItemAsync(CustomerId, cartItemId: 4242)); // not found
        Assert.True(await service.RemoveItemAsync(CustomerId, itemId));
        Assert.Empty(db.Set<CartItem>());
    }

    [Fact]
    public async Task GetCartDetails_EmptyCart_ReturnsNull()
    {
        using var db = TestDb.New();
        Assert.Null(await NewService(db).GetCartDetailsAsync(CustomerId));
    }

    // ---- totals -----------------------------------------------------------

    [Fact]
    public async Task SubTotal_IsSumOfRegularPrices_NoDiscounts()
    {
        using var db = TestDb.New();
        db.Products.AddRange(NewProduct(1, "A", 10m), NewProduct(2, "B", 25m));
        db.SaveChanges();
        var service = NewService(db);
        await service.AddToCartAsync(CustomerId, 1, 2); // 20
        await service.AddToCartAsync(CustomerId, 2, 1); // 25

        var cart = await service.GetCartDetailsAsync(CustomerId);

        Assert.NotNull(cart);
        Assert.Equal(45m, cart!.SubTotal);
        Assert.Equal(0m, cart.Discount);
    }

    [Fact]
    public async Task CatalogSpecialPrice_FoldsSavingIntoDiscount_AndSubTotalUsesRegularPrice()
    {
        using var db = TestDb.New();
        // price 100, active special 75 -> regular (OldPrice) 100, saving 25 per unit.
        db.Products.Add(NewProduct(1, "Sale", 100m, specialPrice: 75m, specialStart: ActiveStart, specialEnd: ActiveEnd));
        db.SaveChanges();
        var service = NewService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);

        var cart = await service.GetCartDetailsAsync(CustomerId);

        Assert.Equal(200m, cart!.SubTotal);   // 2 * regular 100
        Assert.Equal(50m, cart.Discount);     // 2 * (100 - 75)
    }

    [Fact]
    public async Task Coupon_CartFixed_AddsToDiscount_OnTopOfCatalogSaving()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Sale", 100m, specialPrice: 75m, specialStart: ActiveStart, specialEnd: ActiveEnd));
        SeedCartFixedCoupon(db, "SAVE10", amount: 10m);
        db.SaveChanges();
        var service = NewService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);

        var cart = await service.GetCartDetailsAsync(CustomerId, couponCode: "SAVE10");

        Assert.Equal(200m, cart!.SubTotal);
        Assert.Null(cart.CouponValidationErrorMessage);
        Assert.Equal(60m, cart.Discount); // coupon 10 + catalog 50
    }

    [Fact]
    public async Task Coupon_Invalid_RecordsErrorAndOnlyCatalogSavingApplies()
    {
        using var db = TestDb.New();
        db.Products.Add(NewProduct(1, "Sale", 100m, specialPrice: 75m, specialStart: ActiveStart, specialEnd: ActiveEnd));
        db.SaveChanges();
        var service = NewService(db);
        await service.AddToCartAsync(CustomerId, 1, 2);

        var cart = await service.GetCartDetailsAsync(CustomerId, couponCode: "NOPE");

        Assert.NotNull(cart!.CouponValidationErrorMessage);
        Assert.Equal(50m, cart.Discount); // only the catalog saving
    }

    private static void SeedCartFixedCoupon(StoreDbContext db, string code, decimal amount)
    {
        var rule = new CartRule
        {
            Id = 1,
            Name = code,
            IsActive = true,
            RuleToApply = "cart_fixed",
            DiscountAmount = amount
        };
        db.Set<CartRule>().Add(rule);
        db.Set<Coupon>().Add(new Coupon { Id = 1, CartRuleId = rule.Id, CartRule = rule, Code = code });
    }
}
