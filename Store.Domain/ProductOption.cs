using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductOption
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<ProductOptionCombination> ProductOptionCombinations { get; set; } = [];

    public ICollection<ProductOptionValue> ProductOptionValues { get; set; } = [];
}

