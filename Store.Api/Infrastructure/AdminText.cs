namespace Store.Api.Infrastructure;

/// <summary>Text conventions shared by the admin controllers.</summary>
public static class AdminText
{
    /// <summary>
    /// Collapses blank/whitespace input to null, matching how the localized-overlay writer treats
    /// blanks (a null clears the overlay so the base-culture value shows through).
    /// </summary>
    public static string? NormalizeOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
