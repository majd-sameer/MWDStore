using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductCategory
{
    public long Id { get; set; }

    public bool IsFeaturedProduct { get; set; }

    public int DisplayOrder { get; set; }

    public long CategoryId { get; set; }

    public long ProductId { get; set; }

    public Category Category { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

