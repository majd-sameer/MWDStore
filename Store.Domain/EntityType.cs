using System;
using System.Collections.Generic;

namespace Store.Domain;

public class EntityType
{
    public string Id { get; set; } = null!;

    public bool IsMenuable { get; set; }

    public string? AreaName { get; set; }

    public string? RoutingController { get; set; }

    public string? RoutingAction { get; set; }

    public ICollection<Entity> Entities { get; set; } = [];
}

