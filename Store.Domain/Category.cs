using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Category : ISeoEntity, ISoftDeletable
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

    public bool IncludeInMenu { get; set; }

    public bool IsDeleted { get; set; }

    public long? ParentId { get; set; }

    public long? ThumbnailImageId { get; set; }

    public ICollection<ProductCategory> ProductCategories { get; set; } = [];

    public ICollection<Category> InverseParent { get; set; } = [];

    public Category? Parent { get; set; }

    public Medium? ThumbnailImage { get; set; }

    public ICollection<CartRule> CartRules { get; set; } = [];
}

