using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductAttribute
{
    public long Id { get; set; }

    public LocalizedString Name { get; set; } = new();

    public long GroupId { get; set; }

    public ICollection<ProductAttributeValue> ProductAttributeValues { get; set; } = [];

    public ProductAttributeGroup Group { get; set; } = null!;

    public ICollection<ProductTemplate> ProductTemplates { get; set; } = [];
}

