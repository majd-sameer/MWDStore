using System;
using System.Collections.Generic;

namespace Store.Domain;

public class WishListItem
{
    public long Id { get; set; }

    public long WishListId { get; set; }

    public long ProductId { get; set; }

    public string? Description { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public Product Product { get; set; } = null!;

    public WishList WishList { get; set; } = null!;
}

