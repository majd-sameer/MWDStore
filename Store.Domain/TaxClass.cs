using System;
using System.Collections.Generic;

namespace Store.Domain;

public class TaxClass
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = [];

    public ICollection<TaxRate> TaxRates { get; set; } = [];
}

