namespace Store.Application.Payments.Stripe;

/// <summary>
/// Thin port over the Stripe SDK so the gateway service stays free of SDK types and is unit-testable.
/// Covers the slice the Checkout (hosted-page) flow needs: create a Checkout Session, read it back to
/// confirm payment, and verify a webhook signature. The Stripe secret key is supplied per call (it
/// lives per-provider in the DB), so there is no global mutable API key.
/// </summary>
public interface IStripeClient
{
    /// <summary>Creates a Stripe Checkout Session and returns its id and hosted-page URL.</summary>
    Task<StripeSession> CreateCheckoutSessionAsync(
        StripeCheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Checkout Session by id to read its authoritative payment status.</summary>
    Task<StripeSession> GetCheckoutSessionAsync(
        string sessionId, string secretKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a webhook payload's signature and, for <c>checkout.session.completed</c>/
    /// <c>checkout.session.async_payment_*</c> events, returns the affected session; otherwise null.
    /// Throws when the signature is invalid.
    /// </summary>
    StripeSession? ReadCheckoutSessionFromWebhook(string payload, string signatureHeader, string webhookSecret);
}

/// <summary>Inputs for creating a Checkout Session for an order.</summary>
/// <param name="OrderId">Merchant order reference (also set as <c>client_reference_id</c>).</param>
/// <param name="PaymentId">The pending payment row this session settles.</param>
/// <param name="Amount">Order total in the major currency unit (e.g. JOD).</param>
/// <param name="Currency">Lower-case ISO currency (e.g. <c>jod</c>).</param>
/// <param name="Description">Line-item label shown on the Stripe page.</param>
/// <param name="CustomerEmail">Prefilled on the Stripe page (null to let the shopper enter it).</param>
/// <param name="SecretKey">Stripe secret key for this provider.</param>
/// <param name="SuccessUrl">Where Stripe sends the shopper after paying (carries <c>{CHECKOUT_SESSION_ID}</c>).</param>
/// <param name="CancelUrl">Where Stripe sends the shopper if they abandon the page.</param>
public sealed record StripeCheckoutRequest(
    long OrderId,
    long PaymentId,
    decimal Amount,
    string Currency,
    string Description,
    string? CustomerEmail,
    string SecretKey,
    string SuccessUrl,
    string CancelUrl);

/// <summary>The slice of a Stripe Checkout Session the gateway flow acts on.</summary>
/// <param name="Id">Session id (<c>cs_test_…</c>).</param>
/// <param name="Url">Hosted-page URL the shopper is redirected to.</param>
/// <param name="IsPaid">True when <c>payment_status</c> is <c>paid</c> (or <c>no_payment_required</c>).</param>
/// <param name="PaymentIntentId">The underlying PaymentIntent id, when present.</param>
/// <param name="OrderId">Order id read back from session metadata (0 when absent).</param>
public sealed record StripeSession(
    string Id, string? Url, bool IsPaid, string? PaymentIntentId, long OrderId);
