using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Coupon
{
    public long Id { get; set; }

    public long CartRuleId { get; set; }

    public string Code { get; set; } = null!;

    public DateTimeOffset CreatedOn { get; set; }

    public CartRule CartRule { get; set; } = null!;

    public ICollection<CartRuleUsage> CartRuleUsages { get; set; } = [];
}

