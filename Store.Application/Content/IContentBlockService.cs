namespace Store.Application.Content;

/// <summary>Reads/writes for admin-editable homepage <c>ContentBlock</c> rows. English translations
/// are layered on top of the (Arabic) base columns via the same <c>LocalizedContentProperty</c>
/// overlay mechanism catalog entities use — see <c>Store.Application.Localization</c>.</summary>
public interface IContentBlockService
{
    /// <summary>Published blocks, optionally narrowed to a key prefix (e.g. <c>"home"</c> matches
    /// <c>home.hero</c>, <c>home.value.1</c>, ...), ordered by <c>SortOrder</c> then <c>Id</c>,
    /// localized for <paramref name="cultureId"/> (null/unknown culture = base columns only).</summary>
    Task<IReadOnlyList<ContentBlockDto>> GetPublishedAsync(
        string? prefix, string? cultureId, CancellationToken cancellationToken = default);

    /// <summary>All blocks (including unpublished), for the admin list — always includes the raw
    /// English overlay values regardless of request culture.</summary>
    Task<IReadOnlyList<AdminContentBlockDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AdminContentBlockDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Updates the base fields and upserts the English overlay values in one call. Returns
    /// null when no block with <paramref name="id"/> exists.</summary>
    Task<AdminContentBlockDto?> UpdateAsync(
        long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken = default);
}
