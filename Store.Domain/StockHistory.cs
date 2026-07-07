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

    // ----- Stock-out tracking (nullable; existing rows predate the feature) -----

    /// <summary>Set on a tracked stock-out; <c>AdjustedQuantity &lt; 0</c> + <c>Reason != null</c> identifies one.</summary>
    public StockOutReason? Reason { get; set; }

    /// <summary>Required when <see cref="Reason"/> is <see cref="StockOutReason.Sale"/>.</summary>
    public SalesChannel? Channel { get; set; }

    /// <summary>The person who physically took the product out (may differ from <see cref="CreatedById"/>).</summary>
    public long? PerformedById { get; set; }

    /// <summary>Broker name / event name / gift recipient — free text.</summary>
    public string? RecipientOrRef { get; set; }

    public User CreatedBy { get; set; } = null!;

    public User? PerformedBy { get; set; }

    public Product Product { get; set; } = null!;

    public Warehouse Warehouse { get; set; } = null!;
}

