using System;
using System.Collections.Generic;

namespace Store.Domain;

public class OrderItem
{
    public long Id { get; set; }

    public long? OrderId { get; set; }

    public long ProductId { get; set; }

    public decimal ProductPrice { get; set; }

    public int Quantity { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TaxPercent { get; set; }

    public Order? Order { get; set; }

    public Product Product { get; set; } = null!;
}

