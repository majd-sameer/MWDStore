using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Page
{
    public long Id { get; set; }

    public string? Body { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? MetaTitle { get; set; }

    public string? MetaKeywords { get; set; }

    public string? MetaDescription { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedOn { get; set; }

    public bool IsDeleted { get; set; }

    public long CreatedById { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public long LatestUpdatedById { get; set; }

    public User CreatedBy { get; set; } = null!;

    public User LatestUpdatedBy { get; set; } = null!;
}

