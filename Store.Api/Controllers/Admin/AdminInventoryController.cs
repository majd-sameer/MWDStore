using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Inventory;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin warehouse inventory: view per-warehouse stock and apply adjustments via the stock service.</summary>
[ApiController]
[RequirePermission(Permissions.CatalogManage)]
[Route("api/admin/inventory")]
public sealed class AdminInventoryController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IStockService _stockService;

    public AdminInventoryController(StoreDbContext db, IStockService stockService)
    {
        _db = db;
        _stockService = stockService;
    }

    /// <summary>The per-warehouse stock rows for a product, plus its denormalized total.</summary>
    [HttpGet("products/{productId:long}")]
    public async Task<ActionResult<ProductStockDto>> Get(long productId, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Where(p => p.Id == productId)
            .Select(p => new { p.Id, p.Name, p.StockQuantity })
            .FirstOrDefaultAsync(cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        var rows = await _db.Stocks
            .Where(s => s.ProductId == productId)
            .Select(s => new StockRowDto(s.WarehouseId, s.Warehouse.Name, s.Quantity, s.ReservedQuantity))
            .ToListAsync(cancellationToken);

        return Ok(new ProductStockDto(product.Id, product.Name, product.StockQuantity, rows));
    }

    /// <summary>
    /// Adjusts one warehouse's stock for a product (clamped at zero, mirrored to <c>Product.StockQuantity</c>,
    /// audited, and raising back-in-stock on a 0→positive crossing).
    /// </summary>
    [HttpPost("adjust")]
    public async Task<ActionResult<ProductStockDto>> Adjust(
        StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _stockService.UpdateStockAsync(new StockUpdateRequest
            {
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                AdjustedQuantity = request.AdjustedQuantity,
                Note = request.Note,
                UserId = User.GetUserId()
            }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return await Get(request.ProductId, cancellationToken);
    }
}
