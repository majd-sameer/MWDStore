using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Category
{
    public long Id { get; set; }

    public LocalizedString Name { get; set; } = new();

    public string Slug { get; set; } = null!;

    public LocalizedString? MetaTitle { get; set; }

    public LocalizedString? MetaKeywords { get; set; }

    public LocalizedString? MetaDescription { get; set; }

    public LocalizedString? Description { get; set; }

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
