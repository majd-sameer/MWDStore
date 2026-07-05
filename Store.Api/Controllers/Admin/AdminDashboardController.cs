using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Orders;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Read-only analytics for the admin landing page. One round-trip returns the headline KPIs,
/// the revenue/orders trend, the order-status funnel, payment &amp; channel mix, current stock
/// health, the best sellers, and the work queues (low stock + orders needing action).
/// </summary>
[ApiController]
[RequirePermission(Permissions.ReportsView)]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    /// <summary>Statuses that don't count toward realized revenue.</summary>
    private static readonly int[] NonRevenueStatuses =
        [OrderStatus.Canceled, OrderStatus.Refunded];

    /// <summary>Open statuses that still need a human to act on the order.</summary>
    private static readonly int[] ActionStatuses =
        [OrderStatus.New, OrderStatus.OnHold, OrderStatus.PaymentReceived];

    private const int LowStockThreshold = 5;

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdminDashboardController(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <summary>Dashboard aggregates over the last <paramref name="days"/> days (default 30).</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AdminDashboardDto>> Stats(
        [FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);
        var now = _timeProvider.GetUtcNow();
        var firstDay = now.Date.AddDays(-(days - 1));
        var fromDate = new DateTimeOffset(firstDay, now.Offset);

        // Pull the window's master orders once (datasets are small) and aggregate in memory.
        // Project to an anonymous type in SQL, then map to OrderRow client-side.
        var rawOrders = await _db.Orders
            .Where(o => o.ParentId == null && o.CreatedOn >= fromDate)
            .Select(o => new { o.OrderStatus, o.OrderTotal, o.CreatedOn, o.PaymentMethod, o.GuestEmail })
            .ToListAsync(cancellationToken);
        var windowOrders = rawOrders
            .Select(o => new OrderRow(o.OrderStatus, o.OrderTotal, o.CreatedOn, o.PaymentMethod, o.GuestEmail))
            .ToList();

        decimal Revenue(IEnumerable<OrderRow> rows) =>
            rows.Where(r => !NonRevenueStatuses.Contains(r.Status)).Sum(r => r.Total);

        var revenue = Revenue(windowOrders);
        var orderCount = windowOrders.Count;

        // ----- Revenue & orders trend (gap-filled so the line is continuous) -----
        var byDay = windowOrders
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedOn.Date))
            .ToDictionary(g => g.Key, g => new { Revenue = Revenue(g), Orders = g.Count() });

        var trend = new List<AdminTrendPointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var day = DateOnly.FromDateTime(firstDay.AddDays(i));
            trend.Add(byDay.TryGetValue(day, out var v)
                ? new AdminTrendPointDto(day, v.Revenue, v.Orders)
                : new AdminTrendPointDto(day, 0m, 0));
        }

        // ----- Order-status funnel -----
        var statusFunnel = windowOrders
            .GroupBy(r => r.Status)
            .Select(g => new AdminStatusSliceDto(
                g.Key, OrderStatusNames.For(g.Key), g.Count(), g.Sum(r => r.Total)))
            .OrderBy(s => s.Status)
            .ToList();

        // ----- Payment & channel mix -----
        var paymentMix = windowOrders
            .GroupBy(r => string.IsNullOrWhiteSpace(r.PaymentMethod) ? "(none)" : r.PaymentMethod!)
            .Select(g => new AdminNameCountDto(g.Key, g.Count()))
            .OrderByDescending(p => p.Count)
            .ToList();

        var channelMix = new AdminChannelMixDto(
            windowOrders.Count(r => r.GuestEmail != null),
            windowOrders.Count(r => r.GuestEmail == null));

        // ----- Current stock health (all warehouses) -----
        var outOfStock = await _db.Stocks.CountAsync(s => s.Quantity <= 0, cancellationToken);
        var lowCount = await _db.Stocks.CountAsync(
            s => s.Quantity > 0 && s.Quantity <= LowStockThreshold, cancellationToken);
        var healthyCount = await _db.Stocks.CountAsync(s => s.Quantity > LowStockThreshold, cancellationToken);
        var totalUnits = await _db.Stocks.SumAsync(s => (int?)s.Quantity, cancellationToken) ?? 0;
        var stock = new AdminStockHealthDto(outOfStock, lowCount, healthyCount, totalUnits);

        var lowStock = await _db.Stocks
            .Where(s => s.Quantity <= LowStockThreshold && !s.Product.IsDeleted)
            .OrderBy(s => s.Quantity)
            .Take(12)
            .Select(s => new AdminLowStockDto(
                s.ProductId, s.Product.Name, s.Product.Sku, s.Quantity, s.ReservedQuantity))
            .ToListAsync(cancellationToken);

        // ----- Best sellers in the window -----
        // Group by the scalar ProductId only (grouping by a navigation property is not translatable
        // alongside the join filter + order-by-aggregate), then resolve names in a second query.
        var topRaw = await _db.OrderItems
            .Where(oi => oi.OrderId != null && oi.Order!.ParentId == null && oi.Order.CreatedOn >= fromDate)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Units = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.ProductPrice),
            })
            .OrderByDescending(x => x.Units)
            .Take(8)
            .ToListAsync(cancellationToken);

        var topIds = topRaw.Select(x => x.ProductId).ToList();
        var namesById = await _db.Products
            .Where(p => topIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var topProducts = topRaw
            .Select(x => new AdminTopProductDto(
                x.ProductId, namesById.GetValueOrDefault(x.ProductId, string.Empty), x.Units, x.Revenue))
            .ToList();

        // ----- Work queue: open orders needing action (current, not windowed) -----
        var actionRows = await _db.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.ParentId == null && ActionStatuses.Contains(o.OrderStatus))
            .OrderByDescending(o => o.CreatedOn)
            .Take(8)
            .ToListAsync(cancellationToken);
        var actionQueue = actionRows.Select(o => o.ToSummary()).ToList();

        var productCount = await _db.Products.CountAsync(p => !p.IsDeleted, cancellationToken);

        var kpis = new AdminDashboardKpisDto(
            revenue,
            orderCount,
            orderCount > 0 ? Math.Round(revenue / orderCount, 2) : 0m,
            productCount,
            stock.OutOfStock);

        return Ok(new AdminDashboardDto(
            kpis, trend, statusFunnel, paymentMix, channelMix, stock, topProducts, lowStock, actionQueue));
    }

    private readonly record struct OrderRow(
        int Status, decimal Total, DateTimeOffset CreatedOn, string? PaymentMethod, string? GuestEmail);
}
