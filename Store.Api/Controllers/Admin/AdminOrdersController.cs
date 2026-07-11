using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin order management: browse all orders, view detail, change status, cancel (restocks), and refund.</summary>
[ApiController]
[RequirePermission(Permissions.OrdersManage)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IOrderService _orderService;
    private readonly IOrderNotificationService _notifications;
    private readonly IRefundService _refundService;
    private readonly TimeProvider _timeProvider;

    public AdminOrdersController(
        StoreDbContext db, IOrderService orderService, IOrderNotificationService notifications,
        IRefundService refundService, TimeProvider timeProvider)
    {
        _db = db;
        _orderService = orderService;
        _notifications = notifications;
        _refundService = refundService;
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
            .Include(o => o.Payments).ThenInclude(p => p.Refunds)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order == null ? NotFound() : Ok(order.ToDetail() with { PaymentSummary = BuildPaymentSummary(order) });
    }

    /// <summary>
    /// Computes the admin payment rollup the exact way <c>RefundService</c> validates a refund: the settled
    /// captured payment (Succeeded / PartiallyRefunded, latest first), its already-refunded total, and the
    /// remaining refundable balance. Returns null when the order has no captured payment.
    /// </summary>
    private static PaymentSummaryDto? BuildPaymentSummary(Order order)
    {
        var payment = order.Payments
            .Where(p => p.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();

        if (payment == null)
        {
            return null;
        }

        var refunded = payment.Refunds.Sum(r => r.Amount);
        return new PaymentSummaryDto(payment.Amount, refunded, payment.Amount - refunded);
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

        var previousStatus = order.OrderStatus;
        order.OrderStatus = request.OrderStatus;
        order.LatestUpdatedOn = _timeProvider.GetUtcNow();
        order.LatestUpdatedById = User.GetUserId();
        await _db.SaveChangesAsync(cancellationToken);

        if (request.OrderStatus == OrderStatus.Shipped && previousStatus != OrderStatus.Shipped)
        {
            // Best-effort: notification failures never fail the status update (see IOrderNotificationService).
            await _notifications.NotifyOrderShippedAsync(order, cancellationToken);
        }

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

    /// <summary>
    /// Refunds the order's captured payment, in full or in part. Validates against the refundable balance
    /// (captured minus already refunded), executes the refund (Stripe via the gateway; CoD/manual just
    /// records it), advances the payment status, and — when fully refunded — moves the order to Refunded.
    /// Idempotent when <see cref="RefundOrderRequest.IdempotencyKey"/> is supplied. Does NOT restock.
    /// </summary>
    [HttpPost("{id:long}/refund")]
    public async Task<ActionResult<RefundResultDto>> Refund(
        long id, RefundOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _refundService.RefundAsync(
            new RefundRequest(id, request.Amount, request.Reason, User.GetUserId(), request.IdempotencyKey),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        var r = result.Value!;
        return Ok(new RefundResultDto(
            r.RefundId, r.OrderId, r.PaymentId, r.Amount, r.TotalRefunded,
            r.PaymentStatus, r.FullyRefunded, r.ProviderRefundId, r.AlreadyProcessed));
    }
}
