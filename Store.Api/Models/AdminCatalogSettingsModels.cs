using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

public sealed record AdminProductOptionListItem(
    long Id, string Name, string? NameEn,
    /// <summary>True when the option's English name has been translated.</summary>
    bool HasEnglish);

public sealed class ProductOptionUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    // ----- English translation (LocalizedString.En) -------------------------------------------------
    // Base column above holds Arabic. A null/empty value here clears an existing translation
    // (LocalizedString.From normalizes empty to null).
    public string? NameEn { get; set; }
}

// ----- Product attributes (admin CRUD) --------------------------------------------------------------

public sealed record AdminProductAttributeDto(
    long Id, string Name, long GroupId, string GroupName, string? NameEn,
    /// <summary>True when the attribute's English name has been translated.</summary>
    bool HasEnglish);

public sealed class ProductAttributeUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public long GroupId { get; set; }

    // ----- English overlay (LocalizedContentProperty, culture en-US) -------------------------------
    public string? NameEn { get; set; }
}

public sealed record AdminProductAttributeGroupDto(long Id, string Name);

public sealed class ProductAttributeGroupUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

// ----- Categories ---------------------------------------------------------------------------------
