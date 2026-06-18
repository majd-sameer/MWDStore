using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Culture
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ICollection<LocalizedContentProperty> LocalizedContentProperties { get; set; } = [];

    public ICollection<Resource> Resources { get; set; } = [];
}

