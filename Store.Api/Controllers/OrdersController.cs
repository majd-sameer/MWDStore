using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Localization;
using Store.Application.Orders;
using Store.Data;

namespace Store.Api.Controllers;

/// <summary>The signed-in customer's own orders.</summary>
[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly IOrderService _orders;

    public OrdersController(StoreDbContext db, ILocalizationService localization, IOrderService orders)
    {
        _db = db;
        _localization = localization;
        _orders = orders;
    }

    /// <summary>The customer's orders, newest first (master orders only — excludes vendor sub-orders).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> Mine(CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
            .Where(o => o.CustomerId == customerId && o.ParentId == null)
            .OrderByDescending(o => o.CreatedOn)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(o => o.ToSummary()).ToList());
    }

    /// <summary>One of the customer's orders in full.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var order = await _db.Orders
            .AsNoTracking()
            .IncludeDetail()
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == customerId, cancellationToken);

        if (order == null)
        {
            return NotFound();
        }

        var detail = await order.ToDetail()
            .LocalizeItemsAsync(_localization, RequestCulture.OverlayCultureId(Request), cancellationToken);
        return Ok(detail);
    }

    /// <summary>
    /// "Pay again" preflight for an order whose payment failed. When every line is still orderable the
    /// storefront is cleared to start a new payment for this same order; when anything is gone the
    /// whole order is returned to the cart (and canceled, releasing its stock) and the storefront sends
    /// the shopper there — the cart shows what they can still buy, with the rest listed as unavailable
    /// and left out of the totals.
    /// </summary>
    [HttpPost("{id:long}/retry-payment")]
    public async Task<ActionResult<OrderRetryPaymentDto>> RetryPayment(long id, CancellationToken cancellationToken)
    {
        var result = await _orders.RetryPaymentAsync(id, User.GetUserId(), cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        var retry = result.Value!;
        return Ok(new OrderRetryPaymentDto(
            retry.OrderId,
            retry.CanPay,
            retry.MovedToCart,
            retry.UnavailableItems
                .Select(i => new OrderRetryItemDto(
                    i.ProductId, i.ProductName, i.RequestedQuantity, i.AvailableQuantity, i.Reason))
                .ToList()));
    }

    /// <summary>
    /// Public order tracking: looks up a master order's status by its 6-digit tracking number alone.
    /// Anonymous — the code itself is the lookup key (it is random and not guessable from the sequential
    /// order id). Returns 404 when no order has that code.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("track")]
    public async Task<ActionResult<OrderTrackingDto>> Track(
        [FromQuery] string? number, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return BadRequest(new { error = "Tracking number is required." });
        }

        var trackingNumber = number.Trim();
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .Include(o => o.OrderHistories)
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber && o.ParentId == null, cancellationToken);

        if (order == null)
        {
            return NotFound(new { error = "No order matches that tracking number." });
        }

        // Status milestones with dates (notes are intentionally omitted — they can be internal).
        // Fall back to the order's creation as the single milestone when no history was recorded.
        var history = order.OrderHistories
            .OrderBy(h => h.CreatedOn)
            .Select(h => new OrderTrackingEventDto(
                h.NewStatus, OrderStatusNames.For(h.NewStatus), h.CreatedOn))
            .ToList();

        if (history.Count == 0)
        {
            history.Add(new OrderTrackingEventDto(
                order.OrderStatus, OrderStatusNames.For(order.OrderStatus), order.CreatedOn));
        }

        // Strip the guest email (tracking-number-only lookup) and localize line-item names.
        var detail = await (order.ToDetail() with { GuestEmail = null })
            .LocalizeItemsAsync(_localization, RequestCulture.OverlayCultureId(Request), cancellationToken);

        return Ok(new OrderTrackingDto(
            order.Id,
            order.TrackingNumber,
            order.CreatedOn,
            order.OrderStatus,
            OrderStatusNames.For(order.OrderStatus),
            order.OrderTotal,
            order.OrderItems.Sum(i => i.Quantity),
            order.ShippingMethod,
            order.PaymentMethod,
            history,
            detail));
    }
}
