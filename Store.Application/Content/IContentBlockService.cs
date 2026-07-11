namespace Store.Application.Content;

/// <summary>Reads/writes for admin-editable homepage <c>ContentBlock</c> rows. Title/Text/LinkText
/// are bilingual <see cref="Store.Domain.LocalizedString"/> values resolved via the injected
/// <see cref="Store.Application.Localization.IRequestCulture"/> — see <c>ContentBlockService</c>.</summary>
public interface IContentBlockService
{
    /// <summary>Published blocks, optionally narrowed to a key prefix (e.g. <c>"home"</c> matches
    /// <c>home.hero</c>, <c>home.value.1</c>, ...), ordered by <c>SortOrder</c> then <c>Id</c>,
    /// localized for the current request culture.</summary>
    Task<IReadOnlyList<ContentBlockDto>> GetPublishedAsync(
        string? prefix, CancellationToken cancellationToken = default);

    /// <summary>All blocks (including unpublished), for the admin list — always includes the raw
    /// English values regardless of request culture.</summary>
    Task<IReadOnlyList<AdminContentBlockDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AdminContentBlockDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Updates the base fields and the English translations in one call. Returns
    /// null when no block with <paramref name="id"/> exists.</summary>
    Task<AdminContentBlockDto?> UpdateAsync(
        long id, ContentBlockUpdateRequest request, CancellationToken cancellationToken = default);
}
