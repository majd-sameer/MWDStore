using System;
using System.Collections.Generic;

namespace Store.Domain;

public class CatalogRule
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset? StartOn { get; set; }

    public DateTimeOffset? EndOn { get; set; }

    public string? RuleToApply { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public ICollection<CustomerGroup> CustomerGroups { get; set; } = [];
}

