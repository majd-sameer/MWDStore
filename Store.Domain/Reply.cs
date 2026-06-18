using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Reply
{
    public long Id { get; set; }

    public long ReviewId { get; set; }

    public long UserId { get; set; }

    public string? Comment { get; set; }

    public string? ReplierName { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public Review Review { get; set; } = null!;

    public User User { get; set; } = null!;
}

