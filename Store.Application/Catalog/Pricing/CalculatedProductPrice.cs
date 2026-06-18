namespace Store.Application.Catalog.Pricing;

/// <summary>
/// Result of <see cref="IProductPricingService"/>. Port of SimplCommerce's
/// <c>CalculatedProductPrice</c> (the currency-formatting members are intentionally
/// omitted — formatting belongs to a presentation/currency service).
/// </summary>
/// <remarks>
/// Convention preserved from SimplCommerce: <see cref="OldPrice"/> is populated
/// <b>only when there is a discount</b>. <see cref="Price"/> is the effective (discounted)
/// price; <see cref="OldPrice"/> is the pre-discount/regular price. Downstream code keys off
/// <c>OldPrice.HasValue</c> to detect a catalog discount and treats <c>OldPrice ?? Price</c>
/// as the regular line price.
/// </remarks>
public sealed class CalculatedProductPrice
{
    public decimal Price { get; set; }

    public decimal? OldPrice { get; set; }

    public int PercentOfSaving { get; set; }
}
