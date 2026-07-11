using System;
using System.Collections.Generic;

namespace Store.Domain;

public class NewsItem
{
    public long Id { get; set; }

    public LocalizedString? ShortContent { get; set; }

    public LocalizedString? FullContent { get; set; }

    public long? ThumbnailImageId { get; set; }

    public LocalizedString Name { get; set; } = new();

    public string Slug { get; set; } = null!;

    // Meta* stay plain string (effectively unlocalized today) — do not add En meta columns.
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

    public Medium? ThumbnailImage { get; set; }

    public ICollection<NewsCategory> Categories { get; set; } = [];
}
