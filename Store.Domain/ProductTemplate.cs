using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductTemplate
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<ProductAttribute> ProductAttributes { get; set; } = [];
}

