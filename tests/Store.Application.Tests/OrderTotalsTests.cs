using Store.Application.Catalog.Pricing;
using Store.Application.Orders;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.Tax;
using Store.Data;
using Store.Domain;
using static Store.Application.Tests.CheckoutTestSupport;

namespace Store.Application.Tests;

/// <summary>
/// Order-creation totals math: per-line tax,
/// effective price, tax stripping for tax-inclusive prices, coupon + catalog discounts, stock decrement,
/// and the rolled-up totals in a fixed order
/// (discount → shipping → tax → subtotal → subtotal-with-discount → grand total).
/// </summary>
public class OrderTotalsTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveStart = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveEnd = new(2025, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private const string Standard = "Standard";

    private static OrderService NewService(StoreDbContext db, decimal shippingFee = 0m)
    {
        var time = new FixedTimeProvider(Now);
        var shipping = new ConfiguredShippingPriceService(new ShippingOptions
        {
            Methods = [new ShippingMethodSetting { Name = Standard, Price = shippingFee, MinOrderSubtotal = 0m }]
        });
        return new OrderService(
            db, new CouponService(db, time), new TaxService(db), shipping, new ProductPricingService(time), time);
    }

    private static OrderAddressInfo Address(string? zip = null) =>
        new() { CountryId = "US", StateOrProvinceId = 1, ZipCode = zip, ContactName = "Buyer" };

    private static Task<Store.Application.Common.Result<Order>> Create(
        OrderService service, Guid checkoutId, decimal paymentFee = 0m) =>
        service.CreateOrderAsync(checkoutId, "card", paymentFee, Standard, Address(), Address());

    // ---- plain order ------------------------------------------------------

    [Fact]
    public async Task PlainOrder_SumsLines_AddsShippingAndPaymentFee()
    {
        using var db = TestDb.New();
        var a = NewProduct(1, "A", 10m);
        var b = NewProduct(2, "B", 25m);
        db.Products.AddRange(a, b);
        var checkoutId = AddCheckout(db, [(a, 2), (b, 1)]); // 20 + 25 = 45

        var service = NewService(db, shippingFee: 7m);
        var result = await Create(service, checkoutId, paymentFee: 3m);

        Assert.True(result.Success);
        var order = result.Value!;
        Assert.Equal(2, order.OrderItems.Count);
        Assert.Equal(45m, order.SubTotal);
        Assert.Equal(0m, order.TaxAmount);
        Assert.Equal(7m, order.ShippingFeeAmount);
        Assert.Equal(Standard, order.ShippingMethod);
        Assert.Equal(3m, order.PaymentFeeAmount);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(45m, order.SubTotalWithDiscount);
        Assert.Equal(55m, order.OrderTotal); // 45 + 0 + 7 + 3 - 0
        Assert.Equal(OrderStatus.New, order.OrderStatus);
    }

    // ---- tax (exclusive) --------------------------------------------------

    [Fact]
    public async Task TaxExclusivePrices_AddTaxOnTop()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Taxed", 100m, taxClassId: 1);
        db.Products.Add(p);
        AddTaxRate(db, taxClassId: 1, rate: 10m);
        var checkoutId = AddCheckout(db, [(p, 2)]); // not tax-inclusive

        var result = await Create(NewService(db), checkoutId);

        var order = result.Value!;
        var line = Assert.Single(order.OrderItems);
        Assert.Equal(100m, line.ProductPrice);
        Assert.Equal(10m, line.TaxPercent);
        Assert.Equal(20m, line.TaxAmount);  // 2 * 100 * 10%
        Assert.Equal(200m, order.SubTotal);
        Assert.Equal(20m, order.TaxAmount);
        Assert.Equal(220m, order.OrderTotal); // 200 + 20 + 0 + 0 - 0
    }

    // ---- tax (inclusive) --------------------------------------------------

    [Fact]
    public async Task TaxInclusivePrices_StripTaxFromSubtotal()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Inc", 110m, taxClassId: 1);
        db.Products.Add(p);
        AddTaxRate(db, taxClassId: 1, rate: 10m);
        var checkoutId = AddCheckout(db, [(p, 2)], isProductPriceIncludeTax: true);

        var result = await Create(NewService(db), checkoutId);

        var order = result.Value!;
        var line = Assert.Single(order.OrderItems);
        Assert.Equal(100m, line.ProductPrice);       // 110 / 1.1
        Assert.Equal(20m, line.TaxAmount);           // 2 * 100 * 10%
        Assert.Equal(200m, order.SubTotal);          // tax-exclusive
        Assert.Equal(20m, order.TaxAmount);
        Assert.Equal(220m, order.OrderTotal);        // back to the 220 the customer pays
    }

    // ---- catalog special price -------------------------------------------

    [Fact]
    public async Task CatalogSpecialPrice_DiscountsLine_UsingRegularPriceAsBase()
    {
        using var db = TestDb.New();
        // price 100, active special 75 -> base (OldPrice) 100, per-unit saving 25.
        var p = NewProduct(1, "Sale", 100m, specialPrice: 75m, specialStart: ActiveStart, specialEnd: ActiveEnd);
        db.Products.Add(p);
        var checkoutId = AddCheckout(db, [(p, 2)]);

        var result = await Create(NewService(db), checkoutId);

        var order = result.Value!;
        var line = Assert.Single(order.OrderItems);
        Assert.Equal(100m, line.ProductPrice);       // regular price is the line base
        Assert.Equal(50m, line.DiscountAmount);      // 2 * (100 - 75)
        Assert.Equal(200m, order.SubTotal);
        Assert.Equal(50m, order.DiscountAmount);
        Assert.Equal(200m, order.SubTotalWithDiscount); // only coupon is subtracted here (no coupon -> unchanged)
        Assert.Equal(150m, order.OrderTotal);           // 200 + 0 + 0 + 0 - 50
    }

    // ---- coupon -----------------------------------------------------------

    [Fact]
    public async Task CouponCartFixed_SubtractedFromTotalAndSubtotalWithDiscount()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "A", 100m);
        db.Products.Add(p);
        SeedCartFixedCoupon(db, "SAVE30", 30m);
        var checkoutId = AddCheckout(db, [(p, 1)], couponCode: "SAVE30");

        var result = await Create(NewService(db, shippingFee: 5m), checkoutId);

        var order = result.Value!;
        Assert.Equal("SAVE30", order.CouponCode);
        Assert.Equal(100m, order.SubTotal);
        Assert.Equal(30m, order.DiscountAmount);          // coupon only (no per-item discount)
        Assert.Equal(70m, order.SubTotalWithDiscount);    // 100 - 30
        Assert.Equal(75m, order.OrderTotal);              // 100 + 0 + 5 + 0 - 30
    }

    [Fact]
    public async Task CouponPlusCatalogSaving_BothCountTowardDiscount_ButOnlyCouponHitsSubtotalWithDiscount()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Sale", 100m, specialPrice: 75m, specialStart: ActiveStart, specialEnd: ActiveEnd);
        db.Products.Add(p);
        SeedCartFixedCoupon(db, "SAVE10", 10m);
        var checkoutId = AddCheckout(db, [(p, 2)], couponCode: "SAVE10");

        var result = await Create(NewService(db), checkoutId);

        var order = result.Value!;
        Assert.Equal(200m, order.SubTotal);
        Assert.Equal(60m, order.DiscountAmount);          // coupon 10 + catalog 50
        Assert.Equal(190m, order.SubTotalWithDiscount);   // 200 - 10 (coupon only)
        Assert.Equal(140m, order.OrderTotal);             // 200 + 0 + 0 + 0 - 60
    }

    // ---- stock & availability --------------------------------------------

    [Fact]
    public async Task Order_DecrementsTrackedStock()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(p);
        var checkoutId = AddCheckout(db, [(p, 3)]);

        var result = await Create(NewService(db), checkoutId);

        Assert.True(result.Success);
        Assert.Equal(2, db.Products.Single(x => x.Id == 1).StockQuantity);
    }

    [Fact]
    public async Task Order_FailsWhenStockInsufficient()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 1);
        db.Products.Add(p);
        var checkoutId = AddCheckout(db, [(p, 3)]);

        var result = await Create(NewService(db), checkoutId);

        Assert.False(result.Success);
        Assert.Contains("only 1 items available", result.Error);
        Assert.Empty(db.Set<Order>());
        Assert.Equal(1, db.Products.Single(x => x.Id == 1).StockQuantity); // not decremented
    }

    [Fact]
    public async Task Order_FailsWhenProductNotAvailable()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Gone", 10m, allowToOrder: false);
        db.Products.Add(p);
        var checkoutId = AddCheckout(db, [(p, 1)]);

        var result = await Create(NewService(db), checkoutId);

        Assert.False(result.Success);
        Assert.Contains("not available", result.Error);
    }

    [Fact]
    public async Task Order_FailsWhenShippingMethodUnknown()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "A", 10m);
        db.Products.Add(p);
        var checkoutId = AddCheckout(db, [(p, 1)]);

        var service = NewService(db); // only "Standard" is configured
        var result = await service.CreateOrderAsync(checkoutId, "card", 0m, "Express", Address(), Address());

        Assert.False(result.Success);
        Assert.Contains("Invalid shipping method", result.Error);
    }

    // ---- cancel / tax estimate -------------------------------------------

    [Fact]
    public async Task CancelOrder_RestocksTrackedItems()
    {
        using var db = TestDb.New();
        var p = NewProduct(1, "Tracked", 10m, stockTracking: true, stock: 5);
        db.Products.Add(p);
        var checkoutId = AddCheckout(db, [(p, 3)]);
        var service = NewService(db);
        var order = (await Create(service, checkoutId)).Value!;
        Assert.Equal(2, db.Products.Single(x => x.Id == 1).StockQuantity);

        await service.CancelOrderAsync(order);

        Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
        Assert.Equal(5, db.Products.Single(x => x.Id == 1).StockQuantity);
    }

    [Fact]
    public async Task GetTax_SumsOverTaxableCheckoutItems()
    {
        using var db = TestDb.New();
        var taxed = NewProduct(1, "Taxed", 100m, taxClassId: 1);
        var untaxed = NewProduct(2, "Untaxed", 50m);
        db.Products.AddRange(taxed, untaxed);
        AddTaxRate(db, taxClassId: 1, rate: 10m);
        var checkoutId = AddCheckout(db, [(taxed, 2), (untaxed, 4)]);

        var tax = await NewService(db).GetTaxAsync(checkoutId, "US", 1, null);

        Assert.Equal(20m, tax); // 2 * 100 * 10% ; untaxed contributes nothing
    }

    // ---- marketplace sub-orders ------------------------------------------

    [Fact]
    public async Task VendorItems_ProduceMasterOrderWithSubOrderPerVendor()
    {
        using var db = TestDb.New();
        var own = NewProduct(1, "House", 10m);
        var v1 = NewProduct(2, "Vendor1", 40m, vendorId: 100);
        var v2 = NewProduct(3, "Vendor2", 30m, vendorId: 200);
        db.Products.AddRange(own, v1, v2);
        var checkoutId = AddCheckout(db, [(own, 1), (v1, 2), (v2, 1)]);

        var result = await Create(NewService(db), checkoutId);

        var master = result.Value!;
        Assert.True(master.IsMasterOrder);

        var subOrders = db.Set<Order>().Where(o => o.ParentId == master.Id).ToList();
        Assert.Equal(2, subOrders.Count);

        var sub1 = subOrders.Single(o => o.VendorId == 100);
        Assert.Equal(80m, sub1.SubTotal);   // 2 * 40 raw price
        Assert.Equal(80m, sub1.OrderTotal); // no tax/shipping/discount on sub-order
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
        db.SaveChanges();
    }
}
