using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Shipping;

/// <summary>
/// Every enabled <see cref="ShippingProvider"/> contributes its applicable prices. When no
/// provider rows exist yet, falls back to the configured <see cref="ShippingOptions"/> methods
/// so a fresh database still has a working checkout.
/// </summary>
public sealed class DbShippingPriceService : IShippingPriceService
{
    public const string FreeProviderId = "Free";
    public const string TableRateProviderId = "TableRate";
    public const string AramexProviderId = "Aramex";
    public const string JordanPostProviderId = "JordanPost";

    private static readonly JsonSerializerOptions SettingsJson = new() { PropertyNameCaseInsensitive = true };

    private readonly StoreDbContext _db;
    private readonly ShippingOptions _fallbackOptions;

    public DbShippingPriceService(StoreDbContext db, ShippingOptions fallbackOptions)
    {
        _db = db;
        _fallbackOptions = fallbackOptions;
    }

    public async Task<IList<ShippingPrice>> GetApplicableShippingPricesAsync(
        GetShippingPriceRequest request, CancellationToken cancellationToken = default)
    {
        var providers = await _db.ShippingProviders
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .ToListAsync(cancellationToken);

        if (providers.Count == 0)
        {
            return _fallbackOptions.Methods
                .Where(m => request.OrderAmount >= m.MinOrderSubtotal)
                .Select(m => new ShippingPrice(m.Name, m.Price))
                .ToList();
        }

        var cheapestRateByProvider = await GetCheapestTableRatesAsync(
            providers.Where(p => p.Id != FreeProviderId).Select(p => p.Id).ToList(),
            request,
            cancellationToken);

        IList<ShippingPrice> prices = [];
        foreach (var provider in providers)
        {
            if (provider.Id == FreeProviderId)
            {
                AddFreeShippingPrice(prices, request, provider);
            }
            else if (cheapestRateByProvider.TryGetValue(provider.Id, out var price))
            {
                prices.Add(new ShippingPrice(provider.Name, price, provider.Id));
            }
        }

        return prices;
    }

    /// <summary>Free above the configured minimum order amount.</summary>
    private static void AddFreeShippingPrice(
        IList<ShippingPrice> prices, GetShippingPriceRequest request, ShippingProvider provider)
    {
        var setting = ParseFreeShippingSetting(provider.AdditionalSettings);
        if (request.OrderAmount >= setting.MinimumOrderAmount)
        {
            prices.Add(new ShippingPrice(provider.Name, 0m, provider.Id));
        }
    }

    /// <summary>
    /// The cheapest matching <see cref="PriceAndDestination"/> row per provider, resolved in one
    /// query; null destination columns act as wildcards.
    /// </summary>
    private async Task<Dictionary<string, decimal>> GetCheapestTableRatesAsync(
        IReadOnlyCollection<string> providerIds,
        GetShippingPriceRequest request,
        CancellationToken cancellationToken)
    {
        if (providerIds.Count == 0)
        {
            return [];
        }

        var address = request.ShippingAddress;
        var cheapest = await _db.PriceAndDestinations
            .Where(x => x.ShippingProviderId != null
                && providerIds.Contains(x.ShippingProviderId)
                && (x.CountryId == null || x.CountryId == address.CountryId)
                && (x.StateOrProvinceId == null || x.StateOrProvinceId == address.StateOrProvinceId)
                && (x.DistrictId == null || x.DistrictId == address.DistrictId)
                && (string.IsNullOrWhiteSpace(x.ZipCode) || x.ZipCode == address.ZipCode)
                && request.OrderAmount >= x.MinOrderSubtotal)
            .GroupBy(x => x.ShippingProviderId!)
            .Select(g => new { ProviderId = g.Key, Price = g.Min(x => x.ShippingPrice) })
            .ToListAsync(cancellationToken);

        return cheapest.ToDictionary(x => x.ProviderId, x => x.Price);
    }

    public static FreeShippingSetting ParseFreeShippingSetting(string? additionalSettings)
    {
        if (string.IsNullOrWhiteSpace(additionalSettings))
        {
            return new FreeShippingSetting();
        }

        try
        {
            return JsonSerializer.Deserialize<FreeShippingSetting>(additionalSettings, SettingsJson)
                ?? new FreeShippingSetting();
        }
        catch (JsonException)
        {
            return new FreeShippingSetting();
        }
    }
}

/// <summary>Deserialized from the provider's <c>AdditionalSettings</c> JSON.</summary>
public sealed class FreeShippingSetting
{
    public decimal MinimumOrderAmount { get; set; }
}
