using Store.Domain;

namespace Store.Application.Catalog.Pricing;

/// <summary>
/// The single source of truth for "what price do we show / charge" for a product
/// (including variant child products).
/// </summary>
public interface IProductPricingService
{
    CalculatedProductPrice CalculateProductPrice(Product product);

    CalculatedProductPrice CalculateProductPrice(
        decimal price,
        decimal? oldPrice,
        decimal? specialPrice,
        DateTimeOffset? specialPriceStart,
        DateTimeOffset? specialPriceEnd);
}
