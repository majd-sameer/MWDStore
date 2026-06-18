using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Entity
{
    public long Id { get; set; }

    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long EntityId { get; set; }

    public string? EntityTypeId { get; set; }

    public ICollection<MenuItem> MenuItems { get; set; } = [];

    public EntityType? EntityType { get; set; }
}

