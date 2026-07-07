using System;

namespace Store.Domain;

/// <summary>
/// A single editable piece of storefront copy or media, addressed by a stable code triple
/// (<see cref="PageKey"/>/<see cref="SectionKey"/>/<see cref="BlockKey"/>). Design lives in the
/// Angular templates; only the words and images come from here. Keys and <see cref="Type"/> are
/// code-owned (not editable via the API); admins edit <see cref="Value"/>, <see cref="MediumId"/>,
/// <see cref="LinkUrl"/> and <see cref="IsActive"/>. <see cref="Value"/> holds the Arabic default;
/// the English overlay reuses <c>LocalizedContentProperty</c> (entity "ContentBlock", property
/// "Value").
/// </summary>
public class ContentBlock
{
    public long Id { get; set; }

    /// <summary>"home", "about", "contact" — the page the block belongs to.</summary>
    public string PageKey { get; set; } = null!;

    /// <summary>"hero-grid", "mission-band", … — a designed section within the page.</summary>
    public string SectionKey { get; set; } = null!;

    /// <summary>"hero-copy.title", "hero-media", … — the specific slot within the section.</summary>
    public string BlockKey { get; set; } = null!;

    /// <summary>"text" | "richtext" | "image" | "link". Code-owned; not editable via the API.</summary>
    public string Type { get; set; } = null!;

    /// <summary>Arabic-first text (Arabic default, like product data). Null for pure image blocks.</summary>
    public string? Value { get; set; }

    /// <summary>FK <see cref="Medium"/> when <see cref="Type"/> is "image".</summary>
    public long? MediumId { get; set; }

    /// <summary>Target URL when <see cref="Type"/> is "link".</summary>
    public string? LinkUrl { get; set; }

    public bool IsActive { get; set; }

    /// <summary>For repeatable blocks within a section.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset UpdatedOn { get; set; }

    public Medium? Medium { get; set; }
}
