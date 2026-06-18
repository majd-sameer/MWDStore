using System;
using System.Collections.Generic;

namespace Store.Domain;

public class StockHistory
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long CreatedById { get; set; }

    public long AdjustedQuantity { get; set; }

    public string? Note { get; set; }

    public User CreatedBy { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public Warehouse Warehouse { get; set; } = null!;
}

