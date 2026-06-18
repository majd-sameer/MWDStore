using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ActivityType
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = [];
}

