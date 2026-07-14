using System;
using System.Collections.Generic;

namespace Store.Domain;

public class NewsCategory : ISeoEntity, ISoftDeletable
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? MetaTitle { get; set; }

    public string? MetaKeywords { get; set; }

    public string? MetaDescription { get; set; }

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<NewsItem> NewsItems { get; set; } = [];
}

