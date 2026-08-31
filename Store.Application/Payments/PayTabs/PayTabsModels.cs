namespace Store.Application.Payments.PayTabs;

/// <summary>Name and address of the payer / recipient, as PayTabs' <c>customer_details</c> object.</summary>
/// <param name="Country">ISO 3166-1 alpha-2 (PayTabs rejects longer codes).</param>
public sealed record PayTabsParty(
    string? Name,
    string? Email,
    string? Phone,
    string? Street1,
    string? City,
    string? State,
    string? Country,
    string? Zip);

/// <summary>Everything needed to create one hosted payment page.</summary>
/// <param name="BaseUrl">Region API origin (see <see cref="PayTabsRegions"/>).</param>
/// <param name="ProfileId">Merchant profile id from the PayTabs dashboard.</param>
/// <param name="ServerKey">Profile server key — sent as the <c>authorization</c> header.</param>
/// <param name="CartId">Merchant-side reference; must be unique per attempt or PayTabs rejects it as a duplicate.</param>
/// <param name="Currency">Three-letter ISO code, upper-case. Must be enabled on the profile.</param>
/// <param name="Amount">Order total in major units.</param>
/// <param name="ReturnUrl">Where PayTabs form-POSTs the shopper's browser when they finish.</param>
/// <param name="CallbackUrl">Server-to-server IPN target, or null to skip (it must be publicly reachable HTTPS).</param>
/// <param name="Language">Payment page language: <c>en</c> or <c>ar</c>.</param>
public sealed record PayTabsPageRequest(
    string BaseUrl,
    string ProfileId,
    string ServerKey,
    string CartId,
    string CartDescription,
    string Currency,
    decimal Amount,
    string ReturnUrl,
    string? CallbackUrl,
    string Language,
    PayTabsParty? Customer,
    PayTabsParty? Shipping);

/// <summary>A created hosted payment page.</summary>
/// <param name="TranRef">PayTabs transaction reference — the handle for querying and settling.</param>
/// <param name="RedirectUrl">The hosted page to send the shopper to.</param>
public sealed record PayTabsPage(string TranRef, string RedirectUrl);

/// <summary>A transaction's authoritative outcome, as returned by the query endpoint.</summary>
/// <param name="ResponseStatus">Single-letter PayTabs status — see <see cref="PayTabsResponseStatus"/>.</param>
public sealed record PayTabsTransaction(
    string TranRef,
    string? CartId,
    string ResponseStatus,
    string? ResponseCode,
    string? ResponseMessage)
{
    /// <summary>True when the money is secured: authorised, or held pending capture.</summary>
    public bool IsApproved => PayTabsResponseStatus.IsApproved(ResponseStatus);

    /// <summary>
    /// True while the outcome is still unknown (asynchronous payment methods sit here). The payment
    /// must be left pending rather than failed, so a later IPN or retry can still settle it.
    /// </summary>
    public bool IsPending => PayTabsResponseStatus.IsPending(ResponseStatus);
}

/// <summary>The single-letter <c>payment_result.response_status</c> values PayTabs reports.</summary>
public static class PayTabsResponseStatus
{
    /// <summary>Authorised — the success case for a <c>sale</c>.</summary>
    public const string Authorised = "A";

    /// <summary>On hold: authorised but awaiting capture (the success case for an <c>auth</c>).</summary>
    public const string Hold = "H";

    /// <summary>Pending — the shopper hasn't finished, or an async method hasn't reported yet.</summary>
    public const string Pending = "P";

    /// <summary>Declined by the issuer or the fraud rules.</summary>
    public const string Declined = "D";

    /// <summary>Cancelled by the shopper.</summary>
    public const string Cancelled = "C";

    /// <summary>Voided after the fact.</summary>
    public const string Voided = "V";

    /// <summary>Gateway or processing error.</summary>
    public const string Error = "E";

    /// <summary>Expired without a decision.</summary>
    public const string Expired = "X";

    public static bool IsApproved(string? status) =>
        Is(status, Authorised) || Is(status, Hold);

    public static bool IsPending(string? status) =>
        Is(status, Pending) || string.IsNullOrWhiteSpace(status);

    private static bool Is(string? status, string expected) =>
        string.Equals(status?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A PayTabs API call that failed, carrying the gateway's own message where it sent one.</summary>
public sealed class PayTabsException : Exception
{
    public PayTabsException(string message, int? code = null)
        : base(message) => Code = code;

    /// <summary>PayTabs' numeric error code, when the response carried one.</summary>
    public int? Code { get; }
}
