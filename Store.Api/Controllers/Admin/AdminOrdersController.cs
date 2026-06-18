using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Orders;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin order management: browse all orders, view detail, change status, and cancel (restocks).</summary>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IOrderService _orderService;
    private readonly TimeProvider _timeProvider;

    public AdminOrdersController(StoreDbContext db, IOrderService orderService, TimeProvider timeProvider)
    {
        _db = db;
        _orderService = orderService;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> List(
        [FromQuery] int? status, [FromQuery] long? customerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var orders = _db.Orders.Include(o => o.OrderItems).AsQueryable();
        if (status.HasValue)
        {
            orders = orders.Where(o => o.OrderStatus == status.Value);
        }

        if (customerId.HasValue)
        {
            orders = orders.Where(o => o.CustomerId == customerId.Value);
        }

        var items = await orders
            .OrderByDescending(o => o.CreatedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(o => o.ToSummary()).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDetailDto>> Get(long id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order == null ? NotFound() : Ok(order.ToDetail());
    }

    [HttpPut("{id:long}/status")]
    public async Task<ActionResult<OrderDetailDto>> UpdateStatus(
        long id, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        order.OrderStatus = request.OrderStatus;
        order.LatestUpdatedOn = _timeProvider.GetUtcNow();
        order.LatestUpdatedById = User.GetUserId();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(order.ToDetail());
    }

    /// <summary>Cancels the order and restocks each stock-tracked line (SimplCommerce's cancel behavior).</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<OrderDetailDto>> Cancel(long id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        await _orderService.CancelOrderAsync(order, cancellationToken);
        return await Get(id, cancellationToken);
    }
}
