using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Application.Orders;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin order management: browse all orders, view detail, change status, and cancel (restocks).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.OrdersView)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IOrderService _orderService;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditStampReader _auditStamps;

    public AdminOrdersController(
        StoreDbContext db, IOrderService orderService, TimeProvider timeProvider, IAuditStampReader auditStamps)
    {
        _db = db;
        _orderService = orderService;
        _timeProvider = timeProvider;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> List(
        [FromQuery] int[]? statuses, [FromQuery] long? customerId,
        [FromQuery] long? orderNumber, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var orders = _db.Orders.AsNoTracking().Include(o => o.OrderItems).AsQueryable();
        if (statuses is { Length: > 0 })
        {
            orders = orders.Where(o => statuses.Contains(o.OrderStatus));
        }

        if (customerId.HasValue)
        {
            orders = orders.Where(o => o.CustomerId == customerId.Value);
        }

        if (orderNumber.HasValue)
        {
            // The console lists orders by their public tracking number, so match it
            // first; falling back to the internal id keeps old deep links working.
            var tracking = orderNumber.Value.ToString(CultureInfo.InvariantCulture);
            orders = orders.Where(o => o.TrackingNumber == tracking || o.Id == orderNumber.Value);
        }

        if (from.HasValue)
        {
            orders = orders.Where(o => o.CreatedOn >= from.Value);
        }

        if (to.HasValue)
        {
            // `to` is an inclusive end-of-day bound: include the whole day selected.
            var end = to.Value.Date.AddDays(1);
            orders = orders.Where(o => o.CreatedOn < end);
        }

        var result = await orders
            .OrderByDescending(o => o.CreatedOn)
            .ToPagedResultAsync(page, pageSize, o => o.ToSummary(), cancellationToken);

        return Ok(await result.WithAuditStampsAsync(
            _auditStamps, nameof(Order), o => o.Id,
            (o, createdBy, modifiedBy) => o with { CreatedBy = createdBy, ModifiedBy = modifiedBy },
            cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDetailDto>> Get(long id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .IncludeDetail()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order == null ? NotFound() : Ok(order.ToDetail());
    }

    [HttpPut("{id:long}/status")]
    [Authorize(Policy = AuthPolicies.Sales)]
    public async Task<ActionResult<OrderDetailDto>> UpdateStatus(
        long id, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .IncludeDetail()
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

    /// <summary>Cancels the order and restocks each stock-tracked line.</summary>
    [HttpPost("{id:long}/cancel")]
    [Authorize(Policy = AuthPolicies.Sales)]
    public async Task<ActionResult<OrderDetailDto>> Cancel(long id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        await _orderService.CancelOrderAsync(order, "Canceled by staff; stock returned.", cancellationToken);
        return await Get(id, cancellationToken);
    }
}
