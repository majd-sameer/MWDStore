using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

public sealed record AdminPageDto(
    long Id, string Name, string Slug, string? Body, string? MetaTitle, string? MetaKeywords,
    string? MetaDescription, bool IsPublished, DateTimeOffset? PublishedOn, DateTimeOffset CreatedOn,
    string? NameEn, string? BodyEn, string? MetaTitleEn, string? MetaKeywordsEn, string? MetaDescriptionEn,
    /// <summary>True when at least one English overlay property has been translated — drives the
    /// admin list's "EN missing" indicator.</summary>
    bool HasEnglish);

public sealed class PageUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? Body { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsPublished { get; set; } = true;

    // ----- English translation (LocalizedString.En) ---------------------------------------------
    // Base columns above hold Arabic (LocalizedString.Ar). A null/empty value here clears an
    // existing translation (see LocalizedString.From).
    public string? NameEn { get; set; }
    public string? BodyEn { get; set; }
    public string? MetaTitleEn { get; set; }
    public string? MetaKeywordsEn { get; set; }
    public string? MetaDescriptionEn { get; set; }
}
