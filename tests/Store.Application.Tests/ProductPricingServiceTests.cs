using Store.Application.Catalog.Pricing;

namespace Store.Application.Tests;

/// <summary>
/// Sample cases verifying the port of SimplCommerce's
/// <c>ProductPricingService.CalculateProductPrice</c> produces identical results.
/// </summary>
public class ProductPricingServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    // A live special-price window: Start < Now < End.
    private static readonly DateTimeOffset ActiveStart = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveEnd = new(2025, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static ProductPricingService Service() => new(new FixedTimeProvider(Now));

    [Fact]
    public void PlainPrice_NoDiscount()
    {
        var result = Service().CalculateProductPrice(100m, null, null, null, null);

        Assert.Equal(100m, result.Price);
        Assert.Null(result.OldPrice);
        Assert.Equal(0, result.PercentOfSaving);
    }

    [Fact]
    public void ActiveSpecialPrice_BecomesPrice_AndListPriceBecomesOldPrice()
    {
        // price 100, special 75 -> Price 75, OldPrice 100, saving 100 - ceil(75) = 25
        var result = Service().CalculateProductPrice(100m, null, 75m, ActiveStart, ActiveEnd);

        Assert.Equal(75m, result.Price);
        Assert.Equal(100m, result.OldPrice);
        Assert.Equal(25, result.PercentOfSaving);
    }

    [Fact]
    public void ExpiredSpecialPrice_IsIgnored()
    {
        var expiredEnd = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero); // before Now
        var result = Service().CalculateProductPrice(100m, null, 75m, ActiveStart, expiredEnd);

        Assert.Equal(100m, result.Price);
        Assert.Null(result.OldPrice);
        Assert.Equal(0, result.PercentOfSaving);
    }

    [Fact]
    public void NotYetStartedSpecialPrice_IsIgnored()
    {
        var futureStart = new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero); // after Now
        var futureEnd = new DateTimeOffset(2025, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var result = Service().CalculateProductPrice(100m, null, 75m, futureStart, futureEnd);

        Assert.Equal(100m, result.Price);
        Assert.Null(result.OldPrice);
        Assert.Equal(0, result.PercentOfSaving);
    }

    [Fact]
    public void SpecialPrice_WithNullWindow_IsIgnored()
    {
        // Lifted '<' against a null bound is false -> special never applies.
        var result = Service().CalculateProductPrice(100m, null, 75m, null, null);

        Assert.Equal(100m, result.Price);
        Assert.Null(result.OldPrice);
        Assert.Equal(0, result.PercentOfSaving);
    }

    [Fact]
    public void OldPriceHigherThanPrice_YieldsSaving_WithoutSpecial()
    {
        // price 80, oldPrice 100 -> saving 100 - ceil(80) = 20
        var result = Service().CalculateProductPrice(80m, 100m, null, null, null);

        Assert.Equal(80m, result.Price);
        Assert.Equal(100m, result.OldPrice);
        Assert.Equal(20, result.PercentOfSaving);
    }

    [Fact]
    public void OldPriceNotAbovePrice_IsPassedThrough_WithNoSaving()
    {
        // SimplCommerce returns the supplied oldPrice as-is; saving stays 0 because oldPrice <= price.
        var result = Service().CalculateProductPrice(100m, 50m, null, null, null);

        Assert.Equal(100m, result.Price);
        Assert.Equal(50m, result.OldPrice);
        Assert.Equal(0, result.PercentOfSaving);
    }

    [Fact]
    public void ActiveSpecial_KeepsExistingHigherOldPrice()
    {
        // price 100, oldPrice 120 (kept, since 120 >= price), special 75 active
        // saving = 100 - ceil(75/120*100) = 100 - ceil(62.5) = 100 - 63 = 37
        var result = Service().CalculateProductPrice(100m, 120m, 75m, ActiveStart, ActiveEnd);

        Assert.Equal(75m, result.Price);
        Assert.Equal(120m, result.OldPrice);
        Assert.Equal(37, result.PercentOfSaving);
    }

    [Fact]
    public void Saving_UsesCeiling_OnFractionalRatio()
    {
        // price 99.99, special 49.99 active -> 49.99/99.99*100 = 49.995 -> ceil 50 -> saving 50
        var result = Service().CalculateProductPrice(99.99m, null, 49.99m, ActiveStart, ActiveEnd);

        Assert.Equal(49.99m, result.Price);
        Assert.Equal(99.99m, result.OldPrice);
        Assert.Equal(50, result.PercentOfSaving);
    }
}
