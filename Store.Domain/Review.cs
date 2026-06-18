using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Review
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string? Title { get; set; }

    public string? Comment { get; set; }

    public int Rating { get; set; }

    public string? ReviewerName { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public string? EntityTypeId { get; set; }

    public long EntityId { get; set; }

    public ICollection<Reply> Replies { get; set; } = [];

    public User User { get; set; } = null!;
}

