using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

/// <param name="HasEnglish">True when an English <c>Name</c> overlay row exists for this news item.</param>
public sealed record AdminNewsItemListItem(
    long Id, string Name, string Slug, bool IsPublished, DateTimeOffset CreatedOn, string? ThumbnailUrl,
    bool HasEnglish);

public sealed record AdminNewsItemDetail(
    long Id, string Name, string Slug, string? ShortContent, string? FullContent,
    string? MetaTitle, string? MetaKeywords, string? MetaDescription,
    bool IsPublished, long? ThumbnailImageId, string? ThumbnailUrl, IReadOnlyList<long> CategoryIds,
    string? NameEn, string? ShortContentEn, string? FullContentEn);

public sealed class NewsItemUpsertRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }
    public string? ShortContent { get; set; }
    public string? FullContent { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsPublished { get; set; } = true;
    public long? ThumbnailImageId { get; set; }
    public IList<long> CategoryIds { get; set; } = new List<long>();

    // ----- English translation (LocalizedString.En; Meta* stay unlocalized) -----------------------
    public string? NameEn { get; set; }
    public string? ShortContentEn { get; set; }
    public string? FullContentEn { get; set; }
}

// ----- Payments --------------------------------------------------------------------------------------
