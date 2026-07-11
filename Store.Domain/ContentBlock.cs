namespace Store.Domain;

/// <summary>
/// An admin-editable piece of homepage copy/media (e.g. the hero paragraph, the mission-band
/// story, one of the "our values" cards, the newsletter CTA). <see cref="Key"/> is a stable,
/// dotted identifier (e.g. <c>home.hero</c>, <c>home.value.1</c>) that storefront sections look
/// up by prefix. Title/Text/LinkText are bilingual <see cref="LocalizedString"/> values (Arabic in
/// the base column, English in the sibling "...En" column).
/// </summary>
public class ContentBlock
{
    public long Id { get; set; }

    /// <summary>Stable dotted key, e.g. <c>home.hero</c>, <c>home.value.1</c>. Unique.</summary>
    public string Key { get; set; } = null!;

    // Nullable at the EF/DB level (the pre-existing column allows NULL) even though the admin UI
    // always populates it in practice — kept optional to avoid an AlterColumn (NOT NULL) migration.
    public LocalizedString? Title { get; set; }

    /// <summary>Long-form body text (may contain simple HTML).</summary>
    public LocalizedString? Text { get; set; }

    /// <summary>Root-relative media URL (e.g. <c>/user-content/xxx.jpg</c>), or null for none.</summary>
    public string? ImageUrl { get; set; }

    public string? LinkUrl { get; set; }

    public LocalizedString? LinkText { get; set; }

    /// <summary>Ascending display order among blocks that share a prefix.</summary>
    public int SortOrder { get; set; }

    public bool IsPublished { get; set; } = true;
}
