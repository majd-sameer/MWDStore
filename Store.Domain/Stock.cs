using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Stock
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    public int Quantity { get; set; }

    public int ReservedQuantity { get; set; }

    public Product Product { get; set; } = null!;

    public Warehouse Warehouse { get; set; } = null!;
}

