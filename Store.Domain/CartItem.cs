using System;
using System.Collections.Generic;

namespace Store.Domain;

public class CartItem
{
    public long Id { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public long CustomerId { get; set; }

    public long? VendorId { get; set; }

    public User Customer { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

