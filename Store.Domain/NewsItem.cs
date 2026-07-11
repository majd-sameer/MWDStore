using System;
using System.Collections.Generic;

namespace Store.Domain;

public class NewsItem
{
    public long Id { get; set; }

    public string? ShortContent { get; set; }

    public string? FullContent { get; set; }

    public long? ThumbnailImageId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? MetaTitle { get; set; }

    public string? MetaKeywords { get; set; }

    public string? MetaDescription { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset? PublishedOn { get; set; }

    /// <summary>Success stories can link "the story of this product". Null for other categories.</summary>
    public long? ProductId { get; set; }

    /// <summary>Alerts auto-hide from the home band after this UTC time. Null = no expiry.</summary>
    public DateTimeOffset? AlertExpiresOn { get; set; }

    /// <summary>Optional link target for an alert's call-to-action (home band). Null = link to the article.</summary>
    public string? AlertCtaUrl { get; set; }

    public bool IsDeleted { get; set; }

    public long CreatedById { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public long LatestUpdatedById { get; set; }

    public User CreatedBy { get; set; } = null!;

    public User LatestUpdatedBy { get; set; } = null!;

    public Medium? ThumbnailImage { get; set; }

    public Product? Product { get; set; }

    public ICollection<NewsCategory> Categories { get; set; } = [];
}

