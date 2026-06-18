using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Comment
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string? CommentText { get; set; }

    public string? CommenterName { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public string? EntityTypeId { get; set; }

    public long EntityId { get; set; }

    public long? ParentId { get; set; }

    public ICollection<Comment> InverseParent { get; set; } = [];

    public Comment? Parent { get; set; }

    public User User { get; set; } = null!;
}

