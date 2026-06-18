using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Checkout
{
    public Guid Id { get; set; }

    public long CustomerId { get; set; }

    public long CreatedById { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public string? CouponCode { get; set; }

    public string? CouponRuleName { get; set; }

    public string? ShippingMethod { get; set; }

    public bool IsProductPriceIncludeTax { get; set; }

    public decimal? ShippingAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    public long? VendorId { get; set; }

    public string? ShippingData { get; set; }

    public string? OrderNote { get; set; }

    public ICollection<CheckoutItem> CheckoutItems { get; set; } = [];

    public User CreatedBy { get; set; } = null!;

    public User Customer { get; set; } = null!;
}

