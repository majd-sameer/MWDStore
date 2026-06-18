namespace Store.Application.Pricing.Coupons;

/// <summary>
/// Port of SimplCommerce's <c>ICouponService</c>: validates a coupon against a cart and records
/// redemptions when an order is created.
/// </summary>
public interface ICouponService
{
    Task<CouponValidationResult> ValidateAsync(
        long customerId, string couponCode, CartInfoForCoupon cart, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages <c>CartRuleUsage</c> rows for a successful redemption (one for <c>cart_fixed</c>, one per
    /// discounted unit for <c>by_percent</c>). Does not save — the caller commits within the order transaction.
    /// </summary>
    void AddCouponUsage(long customerId, long orderId, CouponValidationResult couponValidationResult);
}
