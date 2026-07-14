namespace Store.Application.Shipping;

/// <summary>
/// Aggregates the applicable shipping methods for a request. The order flow later matches the
/// customer's chosen method by <see cref="ShippingPrice.Name"/>.
/// </summary>
public interface IShippingPriceService
{
    Task<IList<ShippingPrice>> GetApplicableShippingPricesAsync(
        GetShippingPriceRequest request, CancellationToken cancellationToken = default);
}
