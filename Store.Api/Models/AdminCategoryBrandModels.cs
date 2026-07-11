using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

public sealed class CategoryUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IncludeInMenu { get; set; }
    public long? ParentId { get; set; }

    // ----- English translation (LocalizedString.En) ---------------------------------------------
    // Base columns above hold Arabic (LocalizedString.Ar). A null/empty value here clears an
    // existing translation (see LocalizedString.From).
    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? MetaTitleEn { get; set; }
    public string? MetaKeywordsEn { get; set; }
    public string? MetaDescriptionEn { get; set; }
}

public sealed record AdminCategoryDto(
    long Id, string Name, string Slug, string? Description, int DisplayOrder,
    bool IsPublished, bool IncludeInMenu, long? ParentId, bool IsDeleted,
    string? NameEn, string? DescriptionEn, string? MetaTitleEn, string? MetaKeywordsEn, string? MetaDescriptionEn,
    /// <summary>True when at least one English overlay property has been translated — drives the
    /// admin list's "EN missing" indicator.</summary>
    bool HasEnglish);

// ----- Brands -------------------------------------------------------------------------------------

public sealed class BrandUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Description { get; set; }
    public bool IsPublished { get; set; } = true;

    // ----- English overlay (LocalizedContentProperty, culture en-US) -------------------------------
    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }
}

public sealed record AdminBrandDto(
    long Id, string Name, string Slug, string? Description, bool IsPublished, bool IsDeleted,
    string? NameEn, string? DescriptionEn,
    /// <summary>True when at least one English overlay property has been translated.</summary>
    bool HasEnglish);

// ----- Order management ---------------------------------------------------------------------------
