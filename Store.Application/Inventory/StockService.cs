using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Inventory;

/// <summary>
/// Faithful port of SimplCommerce's <c>StockService</c>
/// (<c>Module.Inventory/Services/StockService.cs</c>). Uses an injected <see cref="TimeProvider"/> for the
/// audit timestamp and an <see cref="IProductBackInStockNotifier"/> in place of the MediatR event.
/// </summary>
public sealed class StockService : IStockService
{
    private readonly StoreDbContext _db;
    private readonly IProductBackInStockNotifier _backInStockNotifier;
    private readonly TimeProvider _timeProvider;

    public StockService(StoreDbContext db, IProductBackInStockNotifier backInStockNotifier, TimeProvider timeProvider)
    {
        _db = db;
        _backInStockNotifier = backInStockNotifier;
        _timeProvider = timeProvider;
    }

    public async Task UpdateStockAsync(StockUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} cannot be found");
        var stock = await _db.Set<Stock>()
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.WarehouseId == request.WarehouseId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No stock record for product {request.ProductId} in warehouse {request.WarehouseId}");

        // Clamp a removal so the warehouse stock never goes negative.
        var adjustedQuantity = request.AdjustedQuantity;
        if (adjustedQuantity < 0 && Math.Abs(adjustedQuantity) > stock.Quantity)
        {
            adjustedQuantity = -stock.Quantity;
        }

        var prevStockQuantity = product.StockQuantity;
        stock.Quantity += adjustedQuantity;
        product.StockQuantity += adjustedQuantity;

        // The audit row records the originally requested amount (not the clamped value), as SimplCommerce does.
        _db.Set<StockHistory>().Add(new StockHistory
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            AdjustedQuantity = request.AdjustedQuantity,
            Note = request.Note,
            CreatedById = request.UserId,
            CreatedOn = _timeProvider.GetUtcNow()
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (prevStockQuantity <= 0 && product.StockQuantity > 0)
        {
            await _backInStockNotifier.NotifyAsync(product.Id, cancellationToken);
        }
    }

    public async Task AddAllProductAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        // Vendor's non-option products that don't yet have a stock row in this warehouse get a zero-qty row.
        var existingProductIds = await _db.Set<Stock>()
            .Where(x => x.WarehouseId == warehouse.Id)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        var productIds = await _db.Products
            .Where(x => !x.HasOptions && x.VendorId == warehouse.VendorId && !existingProductIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var stocks = productIds.Select(id => new Stock
        {
            ProductId = id,
            WarehouseId = warehouse.Id,
            Quantity = 0
        });

        _db.Set<Stock>().AddRange(stocks);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
