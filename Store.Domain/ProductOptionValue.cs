using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductOptionValue
{
    public long Id { get; set; }

    public long OptionId { get; set; }

    public long ProductId { get; set; }

    public string? Value { get; set; }

    public string? DisplayType { get; set; }

    public int SortIndex { get; set; }

    public ProductOption Option { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

