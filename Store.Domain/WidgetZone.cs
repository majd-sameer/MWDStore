using System;
using System.Collections.Generic;

namespace Store.Domain;

public class WidgetZone
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public ICollection<WidgetInstance> WidgetInstances { get; set; } = [];
}

