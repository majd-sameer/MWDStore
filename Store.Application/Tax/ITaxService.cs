namespace Store.Application.Tax;

/// <summary>
/// Resolves the tax percent (a whole number, e.g. 10 = 10%) for a product's tax class at a
/// given destination.
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
