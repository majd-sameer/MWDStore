using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Shipping;

/// <summary>
/// Port of SimplCommerce's pluggable shipping pipeline (ShippingPrices + ShippingFree +
/// ShippingTableRate modules): every enabled <see cref="ShippingProvider"/> contributes its
/// applicable prices. When no provider rows exist yet, falls back to the configured
/// <see cref="ShippingOptions"/> methods so a fresh database still has a working checkout.
/// </summary>
public sealed class DbShippingPriceService : IShippingPriceService
{
    /// <summary>Well-known provider ids, matching the old modules' seed rows.</summary>
    public const string FreeProviderId = "Free";
    public const string TableRateProviderId = "TableRate";

    /// <summary>The two carriers offered at checkout, each with its own table-rate rows.</summary>
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
            .Where(p => p.IsEnabled)
            .ToListAsync(cancellationToken);

        if (providers.Count == 0)
        {
            return _fallbackOptions.Methods
                .Where(m => request.OrderAmount >= m.MinOrderSubtotal)
                .Select(m => new ShippingPrice(m.Name, m.Price))
                .ToList();
        }

        IList<ShippingPrice> prices = [];
        foreach (var provider in providers)
        {
            if (provider.Id == FreeProviderId)
            {
                AddFreeShippingPrice(prices, request, provider);
            }
            else
            {
                // Every other provider (TableRate, Aramex, JordanPost, …) prices from its own
                // table-rate rows — the ones tagged with its id.
                await AddTableRatePriceAsync(prices, request, provider, cancellationToken);
            }
        }

        return prices;
    }

    /// <summary>Old <c>FreeShippingServiceProvider</c>: free above the configured minimum order amount.</summary>
    private static void AddFreeShippingPrice(
        IList<ShippingPrice> prices, GetShippingPriceRequest request, ShippingProvider provider)
    {
        var setting = ParseFreeShippingSetting(provider.AdditionalSettings);
        if (request.OrderAmount >= setting.MinimumOrderAmount)
        {
            prices.Add(new ShippingPrice(provider.Name, 0m, provider.Id));
        }
    }

    /// <summary>Old <c>TableRateShippingServiceProvider</c>, now scoped per provider: the cheapest
    /// matching <see cref="PriceAndDestination"/> row owned by <paramref name="provider"/>, where null
    /// columns act as wildcards. The emitted price is labelled with the provider's name so the
    /// storefront can offer each carrier (e.g. Aramex / Jordan Post) as a selectable option.</summary>
    private async Task AddTableRatePriceAsync(
        IList<ShippingPrice> prices, GetShippingPriceRequest request, ShippingProvider provider,
        CancellationToken cancellationToken)
    {
        var address = request.ShippingAddress;
        var rows = await _db.PriceAndDestinations
            .Where(x => x.ShippingProviderId == provider.Id)
            .ToListAsync(cancellationToken);

        var cheapestApplicable = rows
            .Where(x =>
                (x.CountryId == null || x.CountryId == address.CountryId)
                && (x.StateOrProvinceId == null || x.StateOrProvinceId == address.StateOrProvinceId)
                && (x.DistrictId == null || x.DistrictId == address.DistrictId)
                && (string.IsNullOrWhiteSpace(x.ZipCode) || x.ZipCode == address.ZipCode)
                && request.OrderAmount >= x.MinOrderSubtotal)
            .OrderBy(x => x.ShippingPrice)
            .FirstOrDefault();

        if (cheapestApplicable != null)
        {
            prices.Add(new ShippingPrice(provider.Name, cheapestApplicable.ShippingPrice, provider.Id));
        }
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

/// <summary>Mirrors the old ShippingFree module's <c>FreeShippingSetting</c> (JSON in <c>AdditionalSettings</c>).</summary>
public sealed class FreeShippingSetting
{
    public decimal MinimumOrderAmount { get; set; }
}
