namespace Store.Domain;

/// <summary>
/// An admin-editable piece of homepage copy/media (e.g. the hero paragraph, the mission-band
/// story, one of the "our values" cards, the newsletter CTA). <see cref="Key"/> is a stable,
/// dotted identifier (e.g. <c>home.hero</c>, <c>home.value.1</c>) that storefront sections look
/// up by prefix. The entity's own columns hold the base (Arabic) copy, exactly like catalog
/// entities; the English translation is layered on top via <c>LocalizedContentProperty</c>
/// (<c>EntityType = "ContentBlock"</c>) through the same <c>ILocalizationService</c> overlay
/// mechanism used for products and news items.
/// </summary>
public class ContentBlock
{
    public long Id { get; set; }

    /// <summary>Stable dotted key, e.g. <c>home.hero</c>, <c>home.value.1</c>. Unique.</summary>
    public string Key { get; set; } = null!;

    public string? Title { get; set; }

    /// <summary>Long-form body text (may contain simple HTML).</summary>
    public string? Text { get; set; }

    /// <summary>Root-relative media URL (e.g. <c>/user-content/xxx.jpg</c>), or null for none.</summary>
    public string? ImageUrl { get; set; }

    public string? LinkUrl { get; set; }

    public string? LinkText { get; set; }

    /// <summary>Ascending display order among blocks that share a prefix.</summary>
    public int SortOrder { get; set; }

    public bool IsPublished { get; set; } = true;
}
