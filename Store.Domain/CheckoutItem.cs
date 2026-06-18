using System;
using System.Collections.Generic;

namespace Store.Domain;

public class CheckoutItem
{
    public long Id { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public Guid CheckoutId { get; set; }

    public Checkout Checkout { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

