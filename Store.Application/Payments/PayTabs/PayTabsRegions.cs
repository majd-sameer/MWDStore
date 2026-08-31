namespace Store.Application.Payments.PayTabs;

/// <summary>
/// PayTabs is regionally sharded: a merchant account lives in exactly one region and must be called
/// on that region's domain. Calling the wrong one fails authentication ("Authentication failed. Check
/// profile ID and authentication header") rather than returning a useful error, so the region is part
/// of the gateway's saved configuration instead of being guessed.
/// </summary>
/// <remarks>
/// The rule of thumb from PayTabs support: take your merchant dashboard host and swap the
/// <c>merchant</c> label for <c>secure</c> (merchant.paytabs.com → secure.paytabs.com).
/// </remarks>
public static class PayTabsRegions
{
    /// <summary>Madfoat's white-label instance — where the bundled demo profile (crc) lives.</summary>
    public const string Default = "MADFOAT";

    /// <summary>Region code → API origin. Aliases are included so either spelling resolves.</summary>
    private static readonly Dictionary<string, string> BaseUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        // White-label deployments are their own shard: a profile on one is unknown to every regional
        // host (the standard regions reject its server key as an authentication failure).
        ["MADFOAT"] = "https://madfoat-secure.paytabs.com",
        ["ARE"] = "https://secure.paytabs.com",
        ["UAE"] = "https://secure.paytabs.com",
        ["SAU"] = "https://secure.paytabs.sa",
        ["KSA"] = "https://secure.paytabs.sa",
        ["EGY"] = "https://secure-egypt.paytabs.com",
        ["JOR"] = "https://secure-jordan.paytabs.com",
        ["OMN"] = "https://secure-oman.paytabs.com",
        ["KWT"] = "https://secure-kuwait.paytabs.com",
        ["GLOBAL"] = "https://secure-global.paytabs.com"
    };

    /// <summary>Path of the "create hosted payment page" endpoint.</summary>
    public const string RequestPath = "/payment/request";

    /// <summary>Path of the "query transaction" endpoint — the authoritative status source.</summary>
    public const string QueryPath = "/payment/query";

    /// <summary>The API origin for <paramref name="region"/>, falling back to <see cref="Default"/>.</summary>
    public static string BaseUrl(string? region) =>
        BaseUrls.TryGetValue((region ?? string.Empty).Trim(), out var url) ? url : BaseUrls[Default];

    /// <summary>True when <paramref name="region"/> is a code this map knows.</summary>
    public static bool IsKnown(string? region) =>
        !string.IsNullOrWhiteSpace(region) && BaseUrls.ContainsKey(region.Trim());
}
