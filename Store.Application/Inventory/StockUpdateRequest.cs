namespace Store.Application.Inventory;

/// <summary>
/// A request to adjust one product's stock in one warehouse by <see cref="AdjustedQuantity"/>
/// (positive to add, negative to remove). Mirrors SimplCommerce's <c>StockUpdateRequest</c>.
/// </summary>
public sealed class StockUpdateRequest
{
    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    public int AdjustedQuantity { get; set; }

    public string? Note { get; set; }

    public long UserId { get; set; }
}
