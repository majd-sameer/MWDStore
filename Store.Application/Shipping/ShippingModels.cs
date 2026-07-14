using Store.Application.Orders;

namespace Store.Application.Shipping;

/// <summary>A named shipping method and its price, as returned by a rate provider.</summary>
public sealed class ShippingPrice
{
    public ShippingPrice(string name, decimal price, string? providerId = null)
    {
        Name = name;
        Price = price;
        ProviderId = providerId;
    }

    public string Name { get; set; }

    public decimal Price { get; set; }

    /// <summary>The provider (carrier) this price came from, e.g. "Aramex" — used by the storefront to localize the label.</summary>
    public string? ProviderId { get; set; }
}

/// <summary>
/// The order amount (Σ Price·Qty) that threshold rules compare against, plus the destination
/// address that gates geographic availability.
/// </summary>
public sealed class GetShippingPriceRequest
{
    public decimal OrderAmount { get; set; }

    public OrderAddressInfo ShippingAddress { get; set; } = new();
}
