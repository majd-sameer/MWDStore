using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductLink
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public long LinkedProductId { get; set; }

    public int LinkType { get; set; }

    public Product LinkedProduct { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

