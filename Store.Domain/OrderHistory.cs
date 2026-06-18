using System;
using System.Collections.Generic;

namespace Store.Domain;

public class OrderHistory
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public int? OldStatus { get; set; }

    public int NewStatus { get; set; }

    public string? OrderSnapshot { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long CreatedById { get; set; }

    public User CreatedBy { get; set; } = null!;

    public Order Order { get; set; } = null!;
}

