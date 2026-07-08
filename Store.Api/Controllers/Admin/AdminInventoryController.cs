using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Inventory;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin warehouse inventory: view per-warehouse stock and apply adjustments via the stock service.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Inventory)]
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

    /// <summary>
    /// Takes stock out of a warehouse for a business reason (sale, gift, display, …), recording the
    /// reason/channel/performer in <c>StockHistory</c> and an <c>Action = "StockOut"</c> audit row.
    /// The performer defaults to the caller; only admins may log it on someone else's behalf.
    /// </summary>
    [HttpPost("stock-out")]
    [SkipAudit]
    public async Task<ActionResult<ProductStockDto>> StockOut(
        StockOutApiRequest request, CancellationToken cancellationToken)
    {
        var performedById = request.PerformedById;
        if (performedById is not null
            && performedById != User.GetUserId()
            && !User.IsInRole(AppRoles.Admin)
            && !User.IsInRole(AppRoles.SuperAdmin))
        {
            return Forbid();
        }

        var result = await _stockService.StockOutAsync(
            new StockOutRequest
            {
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                Quantity = request.Quantity,
                Reason = request.Reason,
                Channel = request.Channel,
                PerformedById = performedById,
                RecipientOrRef = request.RecipientOrRef,
                Note = request.Note,
            },
            AuditActorFactory.FromContext(HttpContext),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return await Get(request.ProductId, cancellationToken);
    }

    /// <summary>Paged view of tracked stock-outs (StockHistory rows carrying a reason), newest first.</summary>
    [HttpGet("stock-out-log")]
    public async Task<ActionResult<PagedResult<StockOutLogRow>>> StockOutLog(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] StockOutReason? reason = null,
        [FromQuery] SalesChannel? channel = null,
        [FromQuery] long? warehouseId = null,
        [FromQuery] long? performedById = null,
        [FromQuery] string? query = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = _db.Set<StockHistory>().AsNoTracking().Where(h => h.Reason != null);

        if (from is { } fromValue)
        {
            logs = logs.Where(h => h.CreatedOn >= fromValue);
        }

        if (to is { } toValue)
        {
            logs = logs.Where(h => h.CreatedOn <= toValue);
        }

        if (reason is { } reasonValue)
        {
            logs = logs.Where(h => h.Reason == reasonValue);
        }

        if (channel is { } channelValue)
        {
            logs = logs.Where(h => h.Channel == channelValue);
        }

        if (warehouseId is { } warehouseValue)
        {
            logs = logs.Where(h => h.WarehouseId == warehouseValue);
        }

        if (performedById is { } performerValue)
        {
            logs = logs.Where(h => h.PerformedById == performerValue);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            logs = logs.Where(h =>
                h.Product.Name.Contains(term) || (h.RecipientOrRef != null && h.RecipientOrRef.Contains(term)));
        }

        var result = await logs
            .OrderByDescending(h => h.CreatedOn)
            .ThenByDescending(h => h.Id)
            .Select(h => new StockOutLogRow(
                h.Id,
                h.CreatedOn,
                h.ProductId,
                h.Product.Name,
                h.WarehouseId,
                h.Warehouse.Name,
                -h.AdjustedQuantity,
                h.Reason,
                h.Channel,
                h.PerformedById,
                h.PerformedBy != null ? h.PerformedBy.FullName : null,
                h.RecipientOrRef,
                h.Note))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        return Ok(result);
    }
}

/// <summary>Body for <c>POST /api/admin/inventory/stock-out</c>.</summary>
public sealed class StockOutApiRequest
{
    [Required]
    public long ProductId { get; set; }

    [Required]
    public long WarehouseId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    public StockOutReason Reason { get; set; }

    public SalesChannel? Channel { get; set; }

    /// <summary>Optional performer override (admins only); defaults to the caller.</summary>
    public long? PerformedById { get; set; }

    [MaxLength(256)]
    public string? RecipientOrRef { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }
}

public sealed record StockOutLogRow(
    long Id,
    DateTimeOffset CreatedOn,
    long ProductId,
    string ProductName,
    long WarehouseId,
    string WarehouseName,
    long Quantity,
    StockOutReason? Reason,
    SalesChannel? Channel,
    long? PerformedById,
    string? PerformedByName,
    string? RecipientOrRef,
    string? Note);
