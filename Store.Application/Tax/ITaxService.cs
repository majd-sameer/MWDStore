namespace Store.Application.Tax;

/// <summary>
/// Port of SimplCommerce's <c>ITaxService</c>: resolves the tax percent (a whole number, e.g. 10 = 10%)
/// for a product's tax class at a given destination.
/// </summary>
public interface ITaxService
{
    Task<decimal> GetTaxPercentAsync(
        long? taxClassId,
        string? countryId,
        long stateOrProvinceId,
        string? zipCode,
        CancellationToken cancellationToken = default);
}
