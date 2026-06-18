using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Activity
{
    public long Id { get; set; }

    public long ActivityTypeId { get; set; }

    public long UserId { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long EntityId { get; set; }

    public string EntityTypeId { get; set; } = null!;

    public ActivityType ActivityType { get; set; } = null!;
}

