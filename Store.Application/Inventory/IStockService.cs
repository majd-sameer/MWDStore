using Store.Application.Auditing;
using Store.Application.Common;
using Store.Domain;

namespace Store.Application.Inventory;

/// <summary>
/// Warehouse-level stock administration. The denormalized
/// <c>Product.StockQuantity</c> (which cart/order checks read) is kept in sync with the per-warehouse
/// <c>Stock</c> rows. Order creation decrements <c>Product.StockQuantity</c> directly (see OrderService);
/// it does not go through this service.
/// </summary>
public interface IStockService
{
    /// <summary>
    /// Adjusts one warehouse's stock for a product, clamped so warehouse stock never goes negative,
    /// mirrors the delta onto <c>Product.StockQuantity</c>, writes a <c>StockHistory</c> audit row, and
    /// raises back-in-stock if the product crossed from &lt;= 0 to &gt; 0.
    /// </summary>
    Task UpdateStockAsync(StockUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a tracked stock-out: validates the quantity is on hand and that a channel is present
    /// when the reason is <see cref="StockOutReason.Sale"/>, decrements the warehouse <c>Stock</c> and
    /// the denormalized <c>Product.StockQuantity</c>, writes a <c>StockHistory</c> row carrying the
    /// reason/channel/performer, and logs an <c>Action = "StockOut"</c> audit entry. Returns a failed
    /// <see cref="Result"/> (no changes saved) when validation fails.
    /// </summary>
    Task<Result> StockOutAsync(
        StockOutRequest request, AuditActor actor, CancellationToken cancellationToken = default);

    /// <summary>Seeds zero-quantity <c>Stock</c> rows for a warehouse's vendor's non-option products.</summary>
    Task AddAllProductAsync(Warehouse warehouse, CancellationToken cancellationToken = default);
}
