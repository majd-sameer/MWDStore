namespace Store.Application.Localization;

/// <summary>
/// Per-culture content overrides backed by the <c>LocalizedContentProperty</c> table.
/// The base entity columns hold the default-culture text (Arabic for this catalog); an
/// overlay carries the values for another culture (e.g. <c>en-US</c>) for the requested
/// rows, falling back to the base value whenever a translation is missing.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Loads the overrides for <paramref name="entityType"/> rows <paramref name="ids"/> in
    /// <paramref name="cultureId"/>. Returns an empty overlay (base text only) when the culture
    /// is null/empty or there are no ids — so callers can apply it unconditionally.
    /// </summary>
    Task<LocalizedOverlay> GetOverlayAsync(
        string entityType,
        IReadOnlyCollection<long> ids,
        string? cultureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Entity-type discriminators used in <c>LocalizedContentProperty.EntityType</c>.</summary>
public static class LocalizedEntity
{
    public const string Product = "Product";
    public const string NewsItem = "NewsItem";
}

/// <summary>Property-name keys used in <c>LocalizedContentProperty.ProperyName</c>.</summary>
public static class LocalizedProperty
{
    public const string Name = "Name";
    public const string ShortDescription = "ShortDescription";
    public const string Description = "Description";
    public const string ShortContent = "ShortContent";
    public const string FullContent = "FullContent";
}

/// <summary>An immutable bag of (entityId, property) → translated value for one culture.</summary>
public sealed class LocalizedOverlay
{
    public static readonly LocalizedOverlay Empty = new(new Dictionary<(long, string), string>());

    private readonly IReadOnlyDictionary<(long EntityId, string Property), string> _values;

    public LocalizedOverlay(IReadOnlyDictionary<(long, string), string> values) => _values = values;

    public bool IsEmpty => _values.Count == 0;

    /// <summary>The translated value for the row/property, or null when there is no translation.</summary>
    public string? Get(long entityId, string property) =>
        _values.TryGetValue((entityId, property), out var value) ? value : null;

    /// <summary>The translation when present and non-empty, otherwise the supplied base value.</summary>
    public string? Apply(long entityId, string property, string? baseValue)
    {
        var value = Get(entityId, property);
        return string.IsNullOrEmpty(value) ? baseValue : value;
    }
}
