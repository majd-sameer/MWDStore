using System;
using System.Collections.Generic;

namespace Store.Domain;

public class WishList
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string? SharingCode { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public User User { get; set; } = null!;

    public ICollection<WishListItem> WishListItems { get; set; } = [];
}

