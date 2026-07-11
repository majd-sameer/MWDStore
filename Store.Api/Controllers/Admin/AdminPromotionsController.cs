using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin CRUD for cart rules (promotions) and their coupons, the port of the old Pricing module's
/// cart-rule admin. Category/product restrictions map to the CartRuleCategory/CartRuleProduct
/// join tables; usage rows come from checkout's <c>CouponService</c>.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Marketing)]
[Route("api/admin/promotions")]
public sealed class AdminPromotionsController : ControllerBase
{
    private static readonly HashSet<string> SupportedRules = ["cart_fixed", "by_percent"];

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditStampReader _auditStamps;

    public AdminPromotionsController(StoreDbContext db, TimeProvider timeProvider, IAuditStampReader auditStamps)
    {
        _db = db;
        _timeProvider = timeProvider;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCartRuleListItem>>> List(CancellationToken cancellationToken)
    {
        var rules = await _db.CartRules
            .OrderByDescending(r => r.Id)
            .Select(r => new AdminCartRuleListItem(
                r.Id, r.Name, r.IsActive, r.IsCouponRequired, r.RuleToApply,
                r.DiscountAmount, r.StartOn, r.EndOn, r.Coupons.Count, r.CartRuleUsages.Count))
            .ToListAsync(cancellationToken);

        var ids = rules.Select(r => r.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(CartRule), ids, cancellationToken);
        rules = rules
            .Select(r => r with { CreatedBy = stamps.CreatedBy(r.Id), ModifiedBy = stamps.ModifiedBy(r.Id) })
            .ToList();

        return Ok(rules);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminCartRuleDetail>> Get(long id, CancellationToken cancellationToken)
    {
        var rule = await LoadAsync(id, cancellationToken);
        return rule == null ? NotFound() : Ok(ToDetail(rule));
    }

    [HttpPost]
    public async Task<ActionResult<AdminCartRuleDetail>> Create(
        CartRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!SupportedRules.Contains(request.RuleToApply))
        {
            return BadRequest(new { error = "RuleToApply must be 'cart_fixed' or 'by_percent'." });
        }

        var rule = new CartRule { Name = request.Name };
        Apply(rule, request);
        _db.CartRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);

        await ReconcileAsync(rule, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await LoadAsync(rule.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, ToDetail(created!));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminCartRuleDetail>> Update(
        long id, CartRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!SupportedRules.Contains(request.RuleToApply))
        {
            return BadRequest(new { error = "RuleToApply must be 'cart_fixed' or 'by_percent'." });
        }

        var rule = await LoadAsync(id, cancellationToken);
        if (rule == null)
        {
            return NotFound();
        }

        Apply(rule, request);
        await ReconcileAsync(rule, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var updated = await LoadAsync(id, cancellationToken);
        return Ok(ToDetail(updated!));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var rule = await _db.CartRules
            .Include(r => r.Coupons)
            .Include(r => r.Categories)
            .Include(r => r.Products)
            .Include(r => r.CustomerGroups)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule == null)
        {
            return NotFound();
        }

        var hasUsage = await _db.CartRuleUsages.AnyAsync(u => u.CartRuleId == id, cancellationToken);
        if (hasUsage)
        {
            return Conflict(new { error = "This promotion has been used by orders; deactivate it instead of deleting." });
        }

        rule.Categories.Clear();
        rule.Products.Clear();
        rule.CustomerGroups.Clear();
        _db.Coupons.RemoveRange(rule.Coupons);
        _db.CartRules.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Usage rows, optionally filtered to one rule.</summary>
    [HttpGet("usages")]
    public async Task<ActionResult<IReadOnlyList<AdminCartRuleUsageDto>>> Usages(
        [FromQuery] long? cartRuleId, CancellationToken cancellationToken)
    {
        var usages = _db.CartRuleUsages.AsQueryable();
        if (cartRuleId.HasValue)
        {
            usages = usages.Where(u => u.CartRuleId == cartRuleId.Value);
        }

        var items = await usages
            .OrderByDescending(u => u.Id)
            .Take(200)
            .Select(u => new AdminCartRuleUsageDto(
                u.Id, u.CartRuleId, u.CartRule.Name, u.Coupon != null ? u.Coupon.Code : null,
                u.UserId, u.User.Email, u.OrderId, u.CreatedOn))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    // ----- helpers --------------------------------------------------------------------------------

    private Task<CartRule?> LoadAsync(long id, CancellationToken cancellationToken) =>
        _db.CartRules
            .Include(r => r.Coupons)
            .Include(r => r.Categories)
            .Include(r => r.Products)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    private static void Apply(CartRule rule, CartRuleUpsertRequest request)
    {
        rule.Name = request.Name;
        rule.Description = request.Description;
        rule.IsActive = request.IsActive;
        rule.StartOn = request.StartOn;
        rule.EndOn = request.EndOn;
        rule.IsCouponRequired = request.IsCouponRequired;
        rule.RuleToApply = request.RuleToApply;
        rule.DiscountAmount = request.DiscountAmount;
        rule.MaxDiscountAmount = request.MaxDiscountAmount;
        rule.DiscountStep = request.DiscountStep;
        rule.UsageLimitPerCoupon = request.UsageLimitPerCoupon;
        rule.UsageLimitPerCustomer = request.UsageLimitPerCustomer;
    }

    private async Task ReconcileAsync(CartRule rule, CartRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        // Coupon: a single primary code per rule, like the old admin form.
        var code = request.CouponCode?.Trim();
        var existingCoupon = rule.Coupons.FirstOrDefault();
        if (string.IsNullOrEmpty(code))
        {
            if (existingCoupon != null)
            {
                _db.Coupons.Remove(existingCoupon);
            }
        }
        else if (existingCoupon == null)
        {
            var taken = await _db.Coupons.AnyAsync(c => c.Code == code && c.CartRuleId != rule.Id, cancellationToken);
            if (!taken)
            {
                _db.Coupons.Add(new Coupon { CartRule = rule, Code = code, CreatedOn = _timeProvider.GetUtcNow() });
            }
        }
        else if (existingCoupon.Code != code)
        {
            existingCoupon.Code = code;
        }

        // Category restrictions (skip-navigation many-to-many).
        var categories = await _db.Categories
            .Where(c => request.CategoryIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        rule.Categories.Clear();
        foreach (var category in categories)
        {
            rule.Categories.Add(category);
        }

        // Product restrictions.
        var products = await _db.Products
            .Where(p => request.ProductIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        rule.Products.Clear();
        foreach (var product in products)
        {
            rule.Products.Add(product);
        }
    }

    private static AdminCartRuleDetail ToDetail(CartRule r) => new(
        r.Id, r.Name, r.Description, r.IsActive, r.StartOn, r.EndOn,
        r.IsCouponRequired, r.RuleToApply, r.DiscountAmount, r.MaxDiscountAmount,
        r.DiscountStep, r.UsageLimitPerCoupon, r.UsageLimitPerCustomer,
        r.Coupons.FirstOrDefault()?.Code,
        r.Categories.Select(c => c.Id).ToList(),
        r.Products.Select(p => new AdminProductLinkDto(p.Id, p.Name, p.IsPublished)).ToList());
}
