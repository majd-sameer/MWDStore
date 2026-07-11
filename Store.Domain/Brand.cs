using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Brand
{
    public long Id { get; set; }

    public LocalizedString Name { get; set; } = new();

    public string Slug { get; set; } = null!;

    public LocalizedString? Description { get; set; }

    public bool IsPublished { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
