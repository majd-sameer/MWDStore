using System;
using System.Collections.Generic;

namespace Store.Domain;

public class WidgetInstance
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public DateTimeOffset? PublishStart { get; set; }

    public DateTimeOffset? PublishEnd { get; set; }

    public string? WidgetId { get; set; }

    public long WidgetZoneId { get; set; }

    public int DisplayOrder { get; set; }

    public string? Data { get; set; }

    public string? HtmlData { get; set; }

    public Widget? Widget { get; set; }

    public WidgetZone WidgetZone { get; set; } = null!;
}

