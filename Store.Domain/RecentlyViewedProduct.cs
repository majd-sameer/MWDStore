using System;
using System.Collections.Generic;

namespace Store.Domain;

public class RecentlyViewedProduct
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long ProductId { get; set; }

    public DateTimeOffset LatestViewedOn { get; set; }
}

