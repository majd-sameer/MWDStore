namespace Store.Application.Content;

/// <summary>Public shape of a published content block, already localized for the request culture.</summary>
public sealed record ContentBlockDto(
    string Key,
    string? Title,
    string? Text,
    string? ImageUrl,
    string? LinkUrl,
    string? LinkText,
    int SortOrder);

/// <summary>Admin shape: every base field plus the raw (possibly-null) English overlay values,
/// so the edit form can show "no translation yet" instead of a silently-applied fallback.</summary>
public sealed record AdminContentBlockDto(
    long Id,
    string Key,
    string? Title,
    string? Text,
    string? ImageUrl,
    string? LinkUrl,
    string? LinkText,
    int SortOrder,
    bool IsPublished,
    string? TitleEn,
    string? TextEn,
    string? LinkTextEn);

/// <summary>Admin update payload: base (Arabic) fields plus the English overlay fields, written in
/// one call.</summary>
public sealed record ContentBlockUpdateRequest(
    string? Title,
    string? Text,
    string? ImageUrl,
    string? LinkUrl,
    string? LinkText,
    int SortOrder,
    bool IsPublished,
    string? TitleEn,
    string? TextEn,
    string? LinkTextEn);
