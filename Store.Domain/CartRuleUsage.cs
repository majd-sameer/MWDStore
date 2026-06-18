using System;
using System.Collections.Generic;

namespace Store.Domain;

public class CartRuleUsage
{
    public long Id { get; set; }

    public long CartRuleId { get; set; }

    public long? CouponId { get; set; }

    public long UserId { get; set; }

    public long OrderId { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public CartRule CartRule { get; set; } = null!;

    public Coupon? Coupon { get; set; }

    public User User { get; set; } = null!;
}

