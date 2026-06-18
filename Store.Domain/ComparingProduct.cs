using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ComparingProduct
{
    public long Id { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long UserId { get; set; }

    public long ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public User User { get; set; } = null!;
}

