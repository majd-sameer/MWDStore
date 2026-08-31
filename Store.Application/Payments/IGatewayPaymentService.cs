using Store.Application.Common;

namespace Store.Application.Payments;

/// <summary>
/// Payment processing port for the redirect/hosted-page gateways (Stripe, PayPal Express, MEPS).
/// Two-leg flow: <see cref="InitiatePaymentAsync"/> creates a pending <c>Payment</c> and returns
/// where to send the shopper; the gateway later calls back into <see cref="HandleCallbackAsync"/>
/// which records the final result and advances the order.
/// </summary>
/// <remarks>
/// This is a stub shared by all redirect gateways: the sandbox path simulates an approval so the
/// flow is testable end-to-end without live accounts. Per-gateway API/signature work is TODO(payments).
/// </remarks>
public interface IGatewayPaymentService
{
    /// <summary>
    /// Starts a payment for <paramref name="orderId"/> (owned by <paramref name="customerId"/>) via
    /// the provider <paramref name="method"/> (e.g. <c>Stripe</c>): creates a pending payment, moves
    /// the order to <c>PendingPayment</c>, and returns the URL to send the shopper to (the gateway
    /// hosted page, or — in sandbox — a simulated one).
    /// </summary>
    /// <param name="language">
    /// Preferred language for the gateway's hosted page (<c>en</c> / <c>ar</c>); null falls back to
    /// English. Only the gateways that host their own page act on it.
    /// </param>
    Task<Result<GatewayInitiationResult>> InitiatePaymentAsync(
        string method,
        long orderId,
        long customerId,
        string returnUrl,
        string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the gateway callback / return: verifies the signature (skipped in sandbox), marks the
    /// payment succeeded or failed, and advances the order to <c>PaymentReceived</c> / <c>PaymentFailed</c>.
    /// </summary>
    Task<Result<GatewayPaymentResult>> HandleCallbackAsync(
        GatewayCallback callback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a Stripe Checkout payment from its session id, re-querying Stripe for the authoritative
    /// status. Used both by the storefront return page and the webhook; idempotent — re-settling an
    /// already-paid session just returns the existing result. The session id (issued by Stripe and
    /// unguessable) authenticates the request, so no customer identity is required.
    /// </summary>
    Task<Result<GatewayPaymentResult>> SettleStripeSessionAsync(
        string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a PayTabs (MadfoatCom) hosted-page payment from its transaction reference, re-querying
    /// PayTabs for the authoritative status instead of trusting the browser return or the callback
    /// body. Used by both the storefront return page and the server-to-server IPN; idempotent —
    /// re-settling an already-paid transaction just returns the existing result. The <c>tran_ref</c>
    /// (issued by PayTabs and stored on the pending payment) identifies the payment, so no customer
    /// identity is required.
    /// </summary>
    Task<Result<GatewayPaymentResult>> SettlePayTabsTransactionAsync(
        string tranRef, CancellationToken cancellationToken = default);
}

/// <summary>Where to send the shopper after initiating a payment.</summary>
/// <param name="PaymentId">The pending payment row created for this attempt.</param>
/// <param name="OrderId">The order being paid.</param>
/// <param name="Method">The provider id handling the payment.</param>
/// <param name="RedirectUrl">The hosted-page URL (a sandbox simulation when testing).</param>
/// <param name="IsSandbox">True when the simulated sandbox flow is in effect.</param>
public sealed record GatewayInitiationResult(
    long PaymentId, long OrderId, string Method, string RedirectUrl, bool IsSandbox);

/// <summary>Outcome of processing a gateway callback.</summary>
public sealed record GatewayPaymentResult(long PaymentId, long OrderId, bool Approved, string? GatewayTransactionId);

/// <summary>The fields a gateway callback/return is expected to carry. Names are placeholders.</summary>
/// <param name="OrderId">Merchant order reference echoed back by the gateway.</param>
/// <param name="Method">The provider id that processed the payment.</param>
/// <param name="Result">Gateway result code/string (e.g. "APPROVED" / "DECLINED").</param>
/// <param name="GatewayTransactionId">The gateway's transaction id.</param>
/// <param name="Signature">Signature the gateway computed over the payload, for verification.</param>
public sealed record GatewayCallback(
    long OrderId, string Method, string? Result, string? GatewayTransactionId, string? Signature);
