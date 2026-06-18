using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Vendor
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string? Email { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<User> Users { get; set; } = [];

    public ICollection<Warehouse> Warehouses { get; set; } = [];
}

