using System;
using System.Collections.Generic;

namespace Store.Domain;

public class CustomerGroup
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public ICollection<CartRule> CartRules { get; set; } = [];

    public ICollection<CatalogRule> CatalogRules { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}

