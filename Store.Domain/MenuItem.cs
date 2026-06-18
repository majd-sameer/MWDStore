using System;
using System.Collections.Generic;

namespace Store.Domain;

public class MenuItem
{
    public long Id { get; set; }

    public long? ParentId { get; set; }

    public long MenuId { get; set; }

    public long? EntityId { get; set; }

    public string? CustomLink { get; set; }

    public string? Name { get; set; }

    public int DisplayOrder { get; set; }

    public Entity? Entity { get; set; }

    public ICollection<MenuItem> InverseParent { get; set; } = [];

    public Menu Menu { get; set; } = null!;

    public MenuItem? Parent { get; set; }
}

