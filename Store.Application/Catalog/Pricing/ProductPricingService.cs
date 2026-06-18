using Store.Domain;

namespace Store.Application.Catalog.Pricing;

/// <summary>
/// Faithful port of SimplCommerce's <c>ProductPricingService.CalculateProductPrice</c>
/// (<c>Module.Catalog/Services/ProductPricingService.cs</c>).
/// </summary>
/// <remarks>
/// SimplCommerce uses <c>DateTimeOffset.Now</c> (local) directly. Here the clock is injected via
/// <see cref="TimeProvider"/> so the time-boxed special-price window is deterministic in tests;
/// production wiring passes <see cref="TimeProvider.System"/>, which is equivalent.
/// </remarks>
public sealed class ProductPricingService : IProductPricingService
{
    private readonly TimeProvider _timeProvider;

    public ProductPricingService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public CalculatedProductPrice CalculateProductPrice(Product product) =>
        CalculateProductPrice(
            product.Price,
            product.OldPrice,
            product.SpecialPrice,
            product.SpecialPriceStart,
            product.SpecialPriceEnd);

    public CalculatedProductPrice CalculateProductPrice(
        decimal price,
        decimal? oldPrice,
        decimal? specialPrice,
        DateTimeOffset? specialPriceStart,
        DateTimeOffset? specialPriceEnd)
    {
        var percentOfSaving = 0;
        var calculatedPrice = price;
        var now = _timeProvider.GetLocalNow();

        // Special price wins if it is live. Note the nullable comparisons: when either bound is null
        // the lifted '<' operator yields false, so the special price is ignored (matches SimplCommerce).
        if (specialPrice.HasValue && specialPriceStart < now && now < specialPriceEnd)
        {
            calculatedPrice = specialPrice.Value;

            if (!oldPrice.HasValue || oldPrice < price)
            {
                oldPrice = price;
            }
        }

        if (oldPrice.HasValue && oldPrice.Value > 0 && oldPrice > calculatedPrice)
        {
            percentOfSaving = (int)(100 - Math.Ceiling((calculatedPrice / oldPrice.Value) * 100));
        }

        return new CalculatedProductPrice
        {
            Price = calculatedPrice,
            OldPrice = oldPrice,
            PercentOfSaving = percentOfSaving
        };
    }
}
