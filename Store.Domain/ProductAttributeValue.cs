using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductAttributeValue
{
    public long Id { get; set; }

    public long AttributeId { get; set; }

    public long ProductId { get; set; }

    public string? Value { get; set; }

    public ProductAttribute Attribute { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

