using Store.Application.Payments.PayTabs;

namespace Store.Application.Tests;

/// <summary>
/// Covers PayTabs callback/return signature verification — the only thing standing between a forged
/// POST and a settled order, and the part that cannot be exercised without live PayTabs credentials.
/// </summary>
/// <remarks>
/// Every expected digest here was produced by an <b>independent</b> HMAC-SHA256 implementation
/// (Node's <c>crypto</c>) over the canonical string PayTabs' documented PHP reference would build —
/// <c>array_filter</c> → <c>ksort</c> → <c>http_build_query</c>. So these assert cross-implementation
/// agreement with the spec, not that the code agrees with itself.
/// </remarks>
public class PayTabsSignatureTests
{
    private const string ServerKey = "SRVKEY-TEST-0123456789";

    /// <summary>A realistic PayTabs return POST, including the fields PHP's array_filter drops.</summary>
    private static Dictionary<string, string?> ReturnFields() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["respStatus"] = "A",
        ["respCode"] = "G13319",
        ["respMessage"] = "Authorised",
        ["tranRef"] = "TST2306301234567",
        ["cartId"] = "1-1",
        ["cartAmount"] = "14.00",
        ["cartCurrency"] = "AED",
        ["customerEmail"] = "paytabs.test@example.com",
        ["acquirerMessage"] = string.Empty,  // dropped: empty
        ["acquirerRRN"] = "0",               // dropped: PHP treats the string "0" as falsy
        ["token"] = null                     // dropped: null
    };

    /// <summary>
    /// HMAC over
    /// <c>cartAmount=14.00&amp;cartCurrency=AED&amp;cartId=1-1&amp;customerEmail=paytabs.test%40example.com&amp;respCode=G13319&amp;respMessage=Authorised&amp;respStatus=A&amp;tranRef=TST2306301234567</c>.
    /// </summary>
    private const string ReturnSignature = "c9c41916a3ad4f4dd4ee8d7a3f4443c606e3bd36dea19c0bb52183862235d728";

    [Fact]
    public void VerifyReturn_accepts_a_signature_built_the_php_way()
        => Assert.True(PayTabsSignature.VerifyReturn(ReturnFields(), ReturnSignature, ServerKey));

    [Fact]
    public void VerifyReturn_is_case_insensitive_about_the_hex_digest()
        => Assert.True(PayTabsSignature.VerifyReturn(
            ReturnFields(), ReturnSignature.ToUpperInvariant(), ServerKey));

    [Fact]
    public void VerifyReturn_ignores_a_signature_field_already_present_in_the_form()
    {
        // PayTabs posts `signature` alongside the signed fields; it must be excluded from the digest,
        // not hashed into it.
        var fields = ReturnFields();
        fields["signature"] = ReturnSignature;

        Assert.True(PayTabsSignature.VerifyReturn(fields, ReturnSignature, ServerKey));
    }

    [Fact]
    public void VerifyReturn_rejects_a_tampered_amount()
    {
        var fields = ReturnFields();
        fields["cartAmount"] = "0.01";

        Assert.False(PayTabsSignature.VerifyReturn(fields, ReturnSignature, ServerKey));
    }

    [Fact]
    public void VerifyReturn_rejects_an_injected_extra_field()
    {
        var fields = ReturnFields();
        fields["orderId"] = "999";

        Assert.False(PayTabsSignature.VerifyReturn(fields, ReturnSignature, ServerKey));
    }

    [Fact]
    public void VerifyReturn_rejects_the_wrong_server_key()
        => Assert.False(PayTabsSignature.VerifyReturn(ReturnFields(), ReturnSignature, "SRVKEY-WRONG"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-hex-at-all")]
    [InlineData("c9c4")] // valid hex, wrong length — must not throw on the length mismatch
    public void VerifyReturn_rejects_a_missing_or_malformed_signature(string? signature)
        => Assert.False(PayTabsSignature.VerifyReturn(ReturnFields(), signature, ServerKey));

    [Fact]
    public void VerifyReturn_drops_php_falsy_values_from_the_signed_string()
    {
        // The reference digest was computed with acquirerRRN="0" and acquirerMessage="" excluded.
        // Re-including "0" (the natural .NET reading of "not empty") would change the canonical
        // string and break verification, so its absence here is load-bearing, not incidental.
        var fields = ReturnFields();
        Assert.Equal("0", fields["acquirerRRN"]);

        Assert.True(PayTabsSignature.VerifyReturn(fields, ReturnSignature, ServerKey));
    }

    /// <summary>
    /// HMAC over <c>city=%D8%B9%D9%85%D8%A7%D9%86&amp;street=12+King+Hussein+St%7EA.B%2FC</c> —
    /// pins PHP's <c>urlencode</c> rules: space becomes <c>+</c>, <c>~</c> and <c>/</c> are escaped,
    /// <c>.</c> <c>-</c> <c>_</c> are not, and UTF-8 bytes use upper-case hex.
    /// </summary>
    [Fact]
    public void VerifyReturn_uses_php_url_encoding_for_spaces_tildes_and_utf8()
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["city"] = "عمان",
            ["street"] = "12 King Hussein St~A.B/C"
        };

        Assert.True(PayTabsSignature.VerifyReturn(
            fields, "e9fb5a6269145bcfc460c540f53fe967008d14bf20cee1ff05556818f26ff663", ServerKey));
    }

    [Fact]
    public void VerifyReturn_sorts_keys_ordinally_regardless_of_input_order()
    {
        // Same pairs, reversed insertion order: ksort must make the digest order-independent.
        var shuffled = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ReturnFields().Reverse())
        {
            shuffled[entry.Key] = entry.Value;
        }

        Assert.True(PayTabsSignature.VerifyReturn(shuffled, ReturnSignature, ServerKey));
    }

    // --- Callback / IPN: HMAC of the raw body, not of rebuilt fields --------------------------

    private const string CallbackBody =
        """{"tran_ref":"TST2306301234567","cart_id":"1-1","payment_result":{"response_status":"A"}}""";

    private const string CallbackSignature = "a9d91a847593c0e1b0947e43e5378b450b69e79ceafd4e0165ef9286daa39638";

    [Fact]
    public void VerifyCallback_accepts_the_hmac_of_the_raw_body()
        => Assert.True(PayTabsSignature.VerifyCallback(CallbackBody, CallbackSignature, ServerKey));

    [Fact]
    public void VerifyCallback_rejects_a_body_altered_after_signing()
    {
        var tampered = CallbackBody.Replace("\"response_status\":\"A\"", "\"response_status\":\"D\"");

        Assert.False(PayTabsSignature.VerifyCallback(tampered, CallbackSignature, ServerKey));
    }

    [Fact]
    public void VerifyCallback_rejects_a_body_reserialized_with_different_whitespace()
    {
        // The digest covers bytes, not JSON semantics — so the raw body must be hashed before any
        // parse/re-serialize round trip, which this pins.
        var reformatted = CallbackBody.Replace(",", ", ");

        Assert.False(PayTabsSignature.VerifyCallback(reformatted, CallbackSignature, ServerKey));
    }

    [Fact]
    public void VerifyCallback_rejects_the_wrong_server_key()
        => Assert.False(PayTabsSignature.VerifyCallback(CallbackBody, CallbackSignature, "SRVKEY-WRONG"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zzzz")]
    public void VerifyCallback_rejects_a_missing_or_malformed_signature(string? signature)
        => Assert.False(PayTabsSignature.VerifyCallback(CallbackBody, signature, ServerKey));

    [Fact]
    public void Verify_rejects_everything_when_no_server_key_is_configured()
    {
        // An unconfigured provider must fail closed: never treat "no key" as "nothing to check".
        Assert.False(PayTabsSignature.VerifyCallback(CallbackBody, CallbackSignature, string.Empty));
        Assert.False(PayTabsSignature.VerifyReturn(ReturnFields(), ReturnSignature, string.Empty));
    }
}
