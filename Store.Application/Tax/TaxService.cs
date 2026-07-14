using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tax;

/// <summary>
/// First match wins: rows whose <see cref="TaxRate.StateOrProvinceId"/> or
/// <see cref="TaxRate.ZipCode"/> are null/blank act as wildcards.
/// </summary>
public sealed class TaxService : ITaxService
{
    private readonly StoreDbContext _db;

    public TaxService(StoreDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetTaxPercentAsync(
        long? taxClassId,
        string? countryId,
        long stateOrProvinceId,
        string? zipCode,
        CancellationToken cancellationToken = default)
    {
        if (!taxClassId.HasValue)
        {
            return 0;
        }

        var query = _db.Set<TaxRate>().Where(x =>
            x.CountryId == countryId
            && (x.StateOrProvinceId == stateOrProvinceId || x.StateOrProvinceId == null)
            && x.TaxClassId == taxClassId.Value);

        if (!string.IsNullOrEmpty(zipCode))
        {
            query = query.Where(x => x.ZipCode == zipCode || string.IsNullOrWhiteSpace(x.ZipCode));
        }

        var rate = await query
            .Select(x => (decimal?)x.Rate)
            .FirstOrDefaultAsync(cancellationToken);
        return rate ?? 0;
    }
}
