using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Widget
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? ViewComponentName { get; set; }

    public string? CreateUrl { get; set; }

    public string? EditUrl { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public bool IsPublished { get; set; }

    public ICollection<WidgetInstance> WidgetInstances { get; set; } = [];
}

