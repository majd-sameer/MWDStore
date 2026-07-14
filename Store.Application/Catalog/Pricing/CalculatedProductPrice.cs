namespace Store.Application.Catalog.Pricing;

/// <summary>
/// Result of <see cref="IProductPricingService"/>. <see cref="OldPrice"/> is populated
/// <b>only when there is a discount</b>: <see cref="Price"/> is the effective (discounted)
/// price and <see cref="OldPrice"/> the pre-discount/regular price. Downstream code keys off
/// <c>OldPrice.HasValue</c> to detect a catalog discount and treats <c>OldPrice ?? Price</c>
/// as the regular line price.
/// </summary>
public sealed class CalculatedProductPrice
{
    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }

    public int PercentOfSaving { get; set; }
}
