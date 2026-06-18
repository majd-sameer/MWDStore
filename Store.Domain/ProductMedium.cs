using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ProductMedium
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public long MediaId { get; set; }

    public int DisplayOrder { get; set; }

    public Medium Media { get; set; } = null!;

    public Product Product { get; set; } = null!;
}

