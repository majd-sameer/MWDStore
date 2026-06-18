using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductBackInStockSubscription
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string? CustomerEmail { get; set; }
}

