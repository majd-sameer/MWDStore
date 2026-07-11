using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Pricing.Coupons;

/// <summary>
/// Faithful port of SimplCommerce's <c>CouponService</c> (<c>Module.Pricing/Services/CouponService.cs</c>).
/// </summary>
/// <remarks>
/// SimplCommerce reads <c>DateTimeOffset.Now</c> for the rule's active window; here the clock is injected
/// via <see cref="TimeProvider"/> so validation is deterministic in tests. The category scoping is adapted
/// to this model's <c>Product.ProductCategories</c> join (SimplCommerce uses a <c>Product.Categories</c> nav).
/// </remarks>
public sealed class CouponService : ICouponService
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public CouponService(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<CouponValidationResult> ValidateAsync(
        long customerId, string couponCode, CartInfoForCoupon cart, CancellationToken cancellationToken = default)
    {
        var coupon = await _db.Set<Coupon>()
            .Include(x => x.CartRule).ThenInclude(c => c.Products)
            .Include(x => x.CartRule).ThenInclude(c => c.Categories)
            .FirstOrDefaultAsync(x => x.Code == couponCode, cancellationToken);

        var validationResult = new CouponValidationResult { Succeeded = false };

        if (coupon == null || !coupon.CartRule.IsActive)
        {
            validationResult.ErrorMessage = $"The coupon {couponCode} is not exist.";
            return validationResult;
        }

        var now = _timeProvider.GetLocalNow();

        if (coupon.CartRule.StartOn.HasValue && coupon.CartRule.StartOn > now)
        {
            validationResult.ErrorMessage = $"The coupon {couponCode} should be used after {coupon.CartRule.StartOn}.";
            return validationResult;
        }

        if (coupon.CartRule.EndOn.HasValue && coupon.CartRule.EndOn <= now)
        {
            validationResult.ErrorMessage = $"The coupon {couponCode} is expired.";
            return validationResult;
        }

        var couponUsageCount = await _db.Set<CartRuleUsage>().CountAsync(x => x.CouponId == coupon.Id, cancellationToken);
        if (coupon.CartRule.UsageLimitPerCoupon.HasValue && couponUsageCount >= coupon.CartRule.UsageLimitPerCoupon)
        {
            validationResult.ErrorMessage = $"The coupon {couponCode} is all used.";
            return validationResult;
        }

        var couponUsageByCustomerCount = await _db.Set<CartRuleUsage>()
            .CountAsync(x => x.CouponId == coupon.Id && x.UserId == customerId, cancellationToken);
        if (coupon.CartRule.UsageLimitPerCustomer.HasValue && couponUsageByCustomerCount >= coupon.CartRule.UsageLimitPerCustomer)
        {
            validationResult.ErrorMessage = $"You can use the coupon {couponCode} only {coupon.CartRule.UsageLimitPerCustomer} times";
            return validationResult;
        }

        IList<DiscountableProduct> discountableProducts;
        if (!coupon.CartRule.Products.Any() && !coupon.CartRule.Categories.Any())
        {
            // No scope -> every product in the cart is discountable.
            var productIds = cart.Items.Select(x => x.ProductId).ToList();
            discountableProducts = await _db.Products
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new DiscountableProduct { Id = x.Id, Name = x.Name.Ar!, Price = x.Price })
                .ToListAsync(cancellationToken);
        }
        else
        {
            discountableProducts = await GetDiscountableProductsAsync(coupon.CartRule, cancellationToken);
        }

        foreach (var item in cart.Items)
        {
            if ((coupon.CartRule.UsageLimitPerCoupon.HasValue && couponUsageCount >= coupon.CartRule.UsageLimitPerCoupon) ||
                (coupon.CartRule.UsageLimitPerCustomer.HasValue && couponUsageByCustomerCount >= coupon.CartRule.UsageLimitPerCustomer))
            {
                break;
            }

            var discountableProduct = discountableProducts.FirstOrDefault(x => x.Id == item.ProductId);
            if (discountableProduct != null)
            {
                validationResult.DiscountedProducts.Add(new DiscountedProduct
                {
                    Id = discountableProduct.Id,
                    Name = discountableProduct.Name,
                    Price = discountableProduct.Price,
                    Quantity = item.Quantity
                });
            }
        }

        if (!validationResult.DiscountedProducts.Any())
        {
            validationResult.ErrorMessage = $"The coupon {couponCode} doesn't apply to any products in your cart";
            return validationResult;
        }

        validationResult.Succeeded = true;
        validationResult.CouponId = coupon.Id;
        validationResult.CouponCode = coupon.Code;
        validationResult.CouponRuleName = coupon.CartRule.Name;
        validationResult.CartRule = coupon.CartRule;

        switch (coupon.CartRule.RuleToApply)
        {
            case "cart_fixed":
                validationResult.DiscountAmount = Math.Min(
                    coupon.CartRule.DiscountAmount,
                    coupon.CartRule.MaxDiscountAmount.GetValueOrDefault(decimal.MaxValue));
                return validationResult;

            case "by_percent":
                var maxDiscountAmount = coupon.CartRule.MaxDiscountAmount.GetValueOrDefault(decimal.MaxValue);
                foreach (var item in validationResult.DiscountedProducts)
                {
                    item.DiscountAmount = Math.Min((item.Price * coupon.CartRule.DiscountAmount / 100) * item.Quantity, maxDiscountAmount);
                    maxDiscountAmount -= item.DiscountAmount;
                }

                validationResult.DiscountAmount = validationResult.DiscountedProducts.Sum(x => x.DiscountAmount);
                return validationResult;

            default:
                throw new InvalidOperationException($"{coupon.CartRule.RuleToApply} is not supported");
        }
    }

    private async Task<IList<DiscountableProduct>> GetDiscountableProductsAsync(
        CartRule cartRule, CancellationToken cancellationToken)
    {
        IList<DiscountableProduct> discountableProducts = new List<DiscountableProduct>();

        if (cartRule.Products.Any())
        {
            var productIds = cartRule.Products.Select(x => x.Id).ToList();
            discountableProducts = await _db.Products
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new DiscountableProduct { Id = x.Id, Name = x.Name.Ar!, Price = x.Price })
                .ToListAsync(cancellationToken);
        }

        if (cartRule.Categories.Any())
        {
            var categoryIds = cartRule.Categories.Select(x => x.Id).ToList();
            var byCategory = await _db.Products
                .Where(x => x.ProductCategories.Any(c => categoryIds.Contains(c.CategoryId)))
                .Select(x => new DiscountableProduct { Id = x.Id, Name = x.Name.Ar!, Price = x.Price })
                .ToListAsync(cancellationToken);
            discountableProducts = discountableProducts.Concat(byCategory).ToList();
        }

        return discountableProducts;
    }

    public void AddCouponUsage(long customerId, long orderId, CouponValidationResult couponValidationResult)
    {
        if (!couponValidationResult.Succeeded || couponValidationResult.CartRule == null)
        {
            return;
        }

        switch (couponValidationResult.CartRule.RuleToApply)
        {
            case "cart_fixed":
                _db.Set<CartRuleUsage>().Add(NewUsage());
                break;

            case "by_percent":
                foreach (var item in couponValidationResult.DiscountedProducts)
                {
                    for (var i = 0; i < item.Quantity; i++)
                    {
                        _db.Set<CartRuleUsage>().Add(NewUsage());
                    }
                }

                break;

            default:
                throw new InvalidOperationException($"{couponValidationResult.CartRule.RuleToApply} is not supported");
        }

        CartRuleUsage NewUsage() => new()
        {
            UserId = customerId,
            OrderId = orderId,
            CouponId = couponValidationResult.CouponId,
            CartRuleId = couponValidationResult.CartRule!.Id,
            CreatedOn = _timeProvider.GetUtcNow()
        };
    }
}
