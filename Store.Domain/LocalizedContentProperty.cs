using System;
using System.Collections.Generic;

namespace Store.Domain;

public class LocalizedContentProperty
{
    public long Id { get; set; }

    public long EntityId { get; set; }

    public string? EntityType { get; set; }

    public string CultureId { get; set; } = null!;

    public string ProperyName { get; set; } = null!;

    public string? Value { get; set; }

    public Culture Culture { get; set; } = null!;
}

