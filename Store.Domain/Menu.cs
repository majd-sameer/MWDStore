using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Menu
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsPublished { get; set; }

    public bool IsSystem { get; set; }

    public ICollection<MenuItem> MenuItems { get; set; } = [];
}

