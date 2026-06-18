namespace Store.Application.Shipping;

/// <summary>
/// A simple, configurable rate provider standing in for SimplCommerce's pluggable provider system
/// (ShippingFree / ShippingTableRate / etc.). It returns the configured methods whose
/// <see cref="ShippingMethodSetting.MinOrderSubtotal"/> the order amount meets. Real deployments would
/// register a provider that also applies geographic gating; that gating is intentionally out of scope here.
/// </summary>
public sealed class ConfiguredShippingPriceService : IShippingPriceService
{
    private readonly ShippingOptions _options;

    public ConfiguredShippingPriceService(ShippingOptions options)
    {
        _options = options;
    }

    public Task<IList<ShippingPrice>> GetApplicableShippingPricesAsync(
        GetShippingPriceRequest request, CancellationToken cancellationToken = default)
    {
        IList<ShippingPrice> prices = _options.Methods
            .Where(m => request.OrderAmount >= m.MinOrderSubtotal)
            .Select(m => new ShippingPrice(m.Name, m.Price))
            .ToList();

        return Task.FromResult(prices);
    }
}

/// <summary>Configured shipping methods. Defaults to a single free-shipping option.</summary>
public sealed class ShippingOptions
{
    public List<ShippingMethodSetting> Methods { get; set; } =
    [
        new ShippingMethodSetting { Name = "Free shipping", Price = 0m, MinOrderSubtotal = 0m }
    ];
}

public sealed class ShippingMethodSetting
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal MinOrderSubtotal { get; set; }
}
