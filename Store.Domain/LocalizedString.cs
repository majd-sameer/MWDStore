namespace Store.Domain;

/// <summary>Which language the request asked for (Accept-Language: ar|en). Arabic is canonical.</summary>
public enum ContentLanguage { Arabic, English }

/// <summary>Bilingual text. Ar maps to the pre-existing base column; En to the new "&lt;Field&gt;En" column.
/// Mapped as an owned type sharing the owner's table (see LocalizedStringConfiguration).</summary>
public sealed class LocalizedString
{
    public LocalizedString() { }
    public LocalizedString(string? ar, string? en = null) { Ar = ar; En = NullIfEmpty(en); }

    public string? Ar { get; set; }
    public string? En { get; set; }

    public bool HasEnglish => !string.IsNullOrEmpty(En);

    /// <summary>English when asked for and present, otherwise Arabic. Never returns "" for a missing
    /// translation — preserves the old overlay fallback semantics exactly.</summary>
    public string? Resolve(ContentLanguage language) =>
        language == ContentLanguage.English && !string.IsNullOrEmpty(En) ? En : Ar;

    /// <summary>Write-path factory: normalizes empty->null so "clear the English text" round-trips,
    /// and returns null when both parts are empty (optional fields stay absent).</summary>
    public static LocalizedString? From(string? ar, string? en)
    {
        ar = NullIfEmpty(ar); en = NullIfEmpty(en);
        return ar is null && en is null ? null : new LocalizedString(ar, en);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}

/// <summary>Null-tolerant helpers for optional localized fields.</summary>
public static class LocalizedStringExtensions
{
    public static string? Resolve(this LocalizedString? s, ContentLanguage language) => s?.Resolve(language);
    public static bool HasEnglish(this LocalizedString? s) => s?.HasEnglish == true;
}
