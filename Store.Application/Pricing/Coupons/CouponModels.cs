using Store.Domain;

namespace Store.Application.Pricing.Coupons;

/// <summary>The cart snapshot a coupon is validated against: just product ids and quantities.</summary>
public sealed class CartInfoForCoupon
{
    public List<CartItemForCoupon> Items { get; set; } = [];
}

public sealed class CartItemForCoupon
{
    public long ProductId { get; set; }

    public int Quantity { get; set; }
}

/// <summary>A product the coupon's rule is allowed to discount (with its raw list price).</summary>
public sealed class DiscountableProduct
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
}

/// <summary>A discountable product that is actually present in the cart, with the computed line discount.</summary>
public sealed class DiscountedProduct
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Quantity { get; set; }

    public decimal DiscountAmount { get; set; }
}

/// <summary>
/// Outcome of validating a coupon: whether it applied, the total discount, the affected lines, and
/// the rule metadata needed later to record usage.
/// </summary>
public sealed class CouponValidationResult
{
    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }

    public decimal DiscountAmount { get; set; }

    public long CouponId { get; set; }

    public string? CouponCode { get; set; }

    public string? CouponRuleName { get; set; }

    public CartRule? CartRule { get; set; }

    public List<DiscountedProduct> DiscountedProducts { get; set; } = [];
}
