using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Store.Application.Payments.PayTabs;

/// <summary>
/// Verifies the two different signatures PayTabs sends back, both HMAC-SHA256 keyed with the
/// profile's <b>server key</b> but computed over different things:
/// <list type="bullet">
/// <item>
///   <b>Callback / IPN</b> — server-to-server JSON POST. The signature is the hex HMAC of the
///   <i>entire raw request body</i> and arrives in a <c>signature</c> HTTP header.
/// </item>
/// <item>
///   <b>Return</b> — the browser's form POST. The signature is a <c>signature</c> form field, and the
///   signed string is rebuilt from the other form fields: drop empties, sort by key, then join as a
///   URL-encoded query string.
/// </item>
/// </list>
/// </summary>
/// <remarks>
/// The return scheme is defined by PayTabs' own PHP reference implementation
/// (<c>array_filter</c> → <c>ksort</c> → <c>http_build_query</c> → <c>hash_hmac('sha256', …)</c>),
/// so this reproduces PHP's exact semantics rather than the nearest .NET equivalent — see
/// <see cref="IsPhpTruthy"/> and <see cref="PhpUrlEncode"/>, which differ from
/// <c>string.IsNullOrEmpty</c> and <see cref="Uri.EscapeDataString"/> in ways that would silently
/// break verification.
/// </remarks>
public static class PayTabsSignature
{
    /// <summary>The field/header name carrying the signature, in both schemes.</summary>
    public const string FieldName = "signature";

    /// <summary>
    /// Verifies an IPN/callback: HMAC-SHA256 of the raw body, keyed with the server key, compared
    /// against the <c>signature</c> header.
    /// </summary>
    public static bool VerifyCallback(string rawBody, string? signatureHeader, string serverKey)
    {
        if (string.IsNullOrEmpty(serverKey) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        return SignaturesMatch(Hmac(rawBody, serverKey), signatureHeader);
    }

    /// <summary>
    /// Verifies the browser return POST. <paramref name="fields"/> is the posted form (the
    /// <c>signature</c> entry may be present — it is excluded from the signed string either way).
    /// </summary>
    public static bool VerifyReturn(
        IEnumerable<KeyValuePair<string, string?>> fields, string? signature, string serverKey)
    {
        if (string.IsNullOrEmpty(serverKey) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        return SignaturesMatch(Hmac(BuildReturnCanonicalString(fields), serverKey), signature);
    }

    /// <summary>
    /// Rebuilds the string PayTabs signed for a return POST: every field except <c>signature</c>,
    /// PHP-falsy values dropped, sorted by key, rendered as a URL-encoded <c>a=1&amp;b=2</c> query string.
    /// </summary>
    internal static string BuildReturnCanonicalString(IEnumerable<KeyValuePair<string, string?>> fields)
    {
        var signed = fields
            .Where(kv => !string.Equals(kv.Key, FieldName, StringComparison.OrdinalIgnoreCase))
            .Where(kv => IsPhpTruthy(kv.Value))
            // PHP's ksort() on string keys compares byte-wise, which is Ordinal — not culture-aware.
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{PhpUrlEncode(kv.Key)}={PhpUrlEncode(kv.Value!)}");

        return string.Join('&', signed);
    }

    /// <summary>
    /// Mirrors PHP's <c>array_filter($fields)</c> with no callback, which drops every <i>falsy</i>
    /// value — for the strings in a form POST that means <c>null</c>, <c>""</c> <b>and the literal
    /// "0"</b>. Treating "0" as present (the obvious .NET reading) yields a different canonical
    /// string and a signature that never matches.
    /// </summary>
    internal static bool IsPhpTruthy(string? value) =>
        !string.IsNullOrEmpty(value) && !string.Equals(value, "0", StringComparison.Ordinal);

    /// <summary>
    /// PHP's <c>urlencode()</c>, which <c>http_build_query()</c> uses by default (RFC 1738). It
    /// differs from <see cref="Uri.EscapeDataString"/> in two ways that matter here: a space becomes
    /// <c>+</c> rather than <c>%20</c>, and <c>~</c> is escaped while <c>.</c> is not.
    /// </summary>
    internal static string PhpUrlEncode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var sb = new StringBuilder(bytes.Length * 3);

        foreach (var b in bytes)
        {
            if (b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9'
                || b is (byte)'-' or (byte)'_' or (byte)'.')
            {
                sb.Append((char)b);
            }
            else if (b == (byte)' ')
            {
                sb.Append('+');
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }

    /// <summary>Lower-case hex HMAC-SHA256 of <paramref name="data"/> under <paramref name="key"/>.</summary>
    internal static string Hmac(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Constant-time comparison of two hex digests. Compares the decoded bytes so casing doesn't
    /// matter, and returns false (rather than throwing) when the received value isn't valid hex.
    /// </summary>
    private static bool SignaturesMatch(string expectedHex, string receivedHex)
    {
        byte[] received;
        try
        {
            received = Convert.FromHexString(receivedHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHex), received);
    }
}
