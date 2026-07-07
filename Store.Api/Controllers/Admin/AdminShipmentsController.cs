using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Shipment management, the port of the old Shipments module: create a shipment for an order's
/// items from a warehouse (decrementing that warehouse's stock rows + writing stock history,
/// like the old module — <c>Product.StockQuantity</c> was already reduced at order time),
/// list shipments globally or per order, and update tracking numbers.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Fulfillment)]
[Route("api/admin/shipments")]
public sealed class AdminShipmentsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdminShipmentsController(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminShipmentDto>>> List(
        [FromQuery] long? orderId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var shipments = _db.Shipments.AsQueryable();
        if (orderId.HasValue)
        {
            shipments = shipments.Where(s => s.OrderId == orderId.Value);
        }

        var items = await shipments
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new AdminShipmentDto(
                s.Id, s.OrderId, s.TrackingNumber, s.WarehouseId, s.Warehouse.Name, s.CreatedOn,
                s.ShipmentItems.Select(i => new AdminShipmentItemDto(
                    i.Id, i.OrderItemId, i.ProductId, i.Product.Name, i.Quantity)).ToList()))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<AdminShipmentDto>> Create(
        ShipmentCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { error = "A shipment needs at least one item." });
        }

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null)
        {
            return NotFound(new { error = "Order not found." });
        }

        var warehouse = await _db.Warehouses.FindAsync([request.WarehouseId], cancellationToken);
        if (warehouse == null)
        {
            return BadRequest(new { error = "Warehouse not found." });
        }

        var now = _timeProvider.GetUtcNow();
        var userId = User.GetUserId();

        var shipment = new Shipment
        {
            OrderId = order.Id,
            WarehouseId = warehouse.Id,
            TrackingNumber = request.TrackingNumber,
            CreatedById = userId,
            CreatedOn = now,
            LatestUpdatedOn = now
        };

        foreach (var itemRequest in request.Items)
        {
            var orderItem = order.OrderItems.FirstOrDefault(i => i.Id == itemRequest.OrderItemId);
            if (orderItem == null)
            {
                return BadRequest(new { error = $"Order item {itemRequest.OrderItemId} does not belong to order {order.Id}." });
            }

            var alreadyShipped = await _db.ShipmentItems
                .Where(si => si.OrderItemId == orderItem.Id)
                .SumAsync(si => (int?)si.Quantity, cancellationToken) ?? 0;
            if (itemRequest.Quantity + alreadyShipped > orderItem.Quantity)
            {
                return BadRequest(new
                {
                    error = $"Order item {orderItem.Id}: cannot ship {itemRequest.Quantity} (ordered {orderItem.Quantity}, already shipped {alreadyShipped})."
                });
            }

            shipment.ShipmentItems.Add(new ShipmentItem
            {
                OrderItemId = orderItem.Id,
                ProductId = orderItem.ProductId,
                Quantity = itemRequest.Quantity
            });

            // Old Shipments module behavior: shipping pulls from the warehouse's Stock rows.
            // Product.StockQuantity was already decremented when the order was placed.
            var stock = await _db.Stocks.FirstOrDefaultAsync(
                s => s.ProductId == orderItem.ProductId && s.WarehouseId == warehouse.Id, cancellationToken);
            if (stock != null)
            {
                stock.Quantity -= itemRequest.Quantity;
            }

            _db.StockHistories.Add(new StockHistory
            {
                ProductId = orderItem.ProductId,
                WarehouseId = warehouse.Id,
                AdjustedQuantity = -itemRequest.Quantity,
                Note = $"Shipment for order {order.Id}",
                CreatedById = userId,
                CreatedOn = now
            });
        }

        _db.Shipments.Add(shipment);

        // Move the order along like the old admin did once a shipment exists.
        if (order.OrderStatus < Store.Application.Orders.OrderStatus.Shipping)
        {
            order.OrderStatus = Store.Application.Orders.OrderStatus.Shipping;
            order.LatestUpdatedOn = now;
            order.LatestUpdatedById = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var created = await _db.Shipments
            .Where(s => s.Id == shipment.Id)
            .Select(s => new AdminShipmentDto(
                s.Id, s.OrderId, s.TrackingNumber, s.WarehouseId, s.Warehouse.Name, s.CreatedOn,
                s.ShipmentItems.Select(i => new AdminShipmentItemDto(
                    i.Id, i.OrderItemId, i.ProductId, i.Product.Name, i.Quantity)).ToList()))
            .FirstAsync(cancellationToken);

        return Ok(created);
    }

    [HttpPut("{id:long}/tracking")]
    public async Task<IActionResult> UpdateTracking(
        long id, [FromBody] string? trackingNumber, CancellationToken cancellationToken)
    {
        var shipment = await _db.Shipments.FindAsync([id], cancellationToken);
        if (shipment == null)
        {
            return NotFound();
        }

        shipment.TrackingNumber = trackingNumber;
        shipment.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
