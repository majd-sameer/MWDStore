using System;
using System.Collections.Generic;

namespace Store.Domain;

public class CartRule
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset? StartOn { get; set; }

    public DateTimeOffset? EndOn { get; set; }

    public bool IsCouponRequired { get; set; }

    public string? RuleToApply { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public int? DiscountStep { get; set; }

    public int? UsageLimitPerCoupon { get; set; }

    public int? UsageLimitPerCustomer { get; set; }

    public ICollection<CartRuleUsage> CartRuleUsages { get; set; } = [];

    public ICollection<Coupon> Coupons { get; set; } = [];

    public ICollection<Category> Categories { get; set; } = [];

    public ICollection<CustomerGroup> CustomerGroups { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];
}

