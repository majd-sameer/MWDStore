using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Query
{
    public long Id { get; set; }

    public string QueryText { get; set; } = null!;

    public int ResultsCount { get; set; }

    public DateTimeOffset CreatedOn { get; set; }
}

