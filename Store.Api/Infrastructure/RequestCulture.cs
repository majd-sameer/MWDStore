namespace Store.Api.Infrastructure;

/// <summary>
/// Resolves which culture's content overlay to apply for a request. Content is bilingual via
/// <c>LocalizedContentProperty</c>: English overrides under <c>en-US</c>, and Arabic overrides under
/// <c>arabic</c> for the rows whose base columns aren't fully Arabic (some source names/descriptions
/// contain English). When no override exists the base column is used as the fallback. The
/// storefront's Accept-Language interceptor sends a bare "en" or "ar".
/// </summary>
public static class RequestCulture
{
    /// <summary>Culture id of the English overrides stored in <c>LocalizedContentProperty</c>.</summary>
    public const string EnglishCultureId = "en-US";

    /// <summary>Culture id of the Arabic overrides (used to scrub English out of the Arabic view).</summary>
    public const string ArabicCultureId = "arabic";

    /// <summary>
    /// The culture id to overlay for this request, or null to serve the base columns unchanged.
    /// </summary>
    public static string? OverlayCultureId(HttpRequest request)
    {
        var acceptLanguage = request.Headers.AcceptLanguage.ToString();
        if (acceptLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return EnglishCultureId;
        }
        if (acceptLanguage.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
        {
            return ArabicCultureId;
        }
        return null;
    }
}
