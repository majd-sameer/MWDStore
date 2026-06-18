using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Resource
{
    public long Id { get; set; }

    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public string CultureId { get; set; } = null!;

    public Culture Culture { get; set; } = null!;
}

