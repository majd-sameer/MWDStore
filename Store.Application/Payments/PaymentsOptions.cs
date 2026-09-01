namespace Store.Application.Payments;

/// <summary>
/// Host-configured payment options. Bound from the <c>Payments</c> configuration section in
/// <c>Store.Api</c>; defaults target the local storefront dev server so the Stripe sandbox cycle
/// works out of the box.
/// </summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Origin of the storefront SPA, used to build the absolute Stripe Checkout
    /// <c>success_url</c>/<c>cancel_url</c> the shopper returns to (no trailing slash).
    /// </summary>
    public string StorefrontBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Publicly reachable origin of this API (no trailing slash), used to build the absolute
    /// <c>return</c> and <c>callback</c> URLs handed to PayTabs.
    /// </summary>
    /// <remarks>
    /// The two URLs are reached very differently, which is why the localhost default still works for
    /// development. <c>return</c> is a form POST made by the <i>shopper's browser</i>, so it resolves
    /// fine against a local API. <c>callback</c> is a server-to-server POST from PayTabs' own
    /// infrastructure, so it can never reach a private address — the IPN simply doesn't fire in dev,
    /// and the return page's verify call settles the payment instead. Point this at the real public
    /// origin in production to get the IPN as a safety net for shoppers who close the tab.
    /// </remarks>
    public string PublicApiBaseUrl { get; set; } = "https://localhost:7142";

    /// <summary>
    /// How long a hosted-page payment may sit unresolved before the reconciliation sweep gives up on
    /// it: the attempt is voided (<see cref="PaymentStatus.Voided"/>) and its order is canceled and
    /// restocked. Counted from the moment the shopper was sent to the gateway — PayTabs' own page
    /// expires well inside the 30-minute default.
    /// </summary>
    public int PendingPaymentTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// How often the reconciliation sweep runs. It exists so settlement never depends on the shopper's
    /// browser coming back: someone who pays and closes the tab is still settled here, including where
    /// the server-to-server IPN can't be delivered (localhost, or a blocked callback).
    /// </summary>
    public int ReconciliationIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How long an attempt is left alone before the sweep starts querying the gateway about it. Keeps
    /// the sweep from racing a shopper who is still on the hosted page, whose browser return is what
    /// normally settles first.
    /// </summary>
    public int ReconciliationGraceMinutes { get; set; } = 2;

    /// <summary>Kill switch for the sweep. Off leaves settlement to the return leg and the IPN alone.</summary>
    public bool ReconciliationEnabled { get; set; } = true;

    /// <summary><see cref="PendingPaymentTimeoutMinutes"/> as a timespan, floored at one minute.</summary>
    public TimeSpan PendingPaymentTimeout => TimeSpan.FromMinutes(Math.Max(1, PendingPaymentTimeoutMinutes));

    /// <summary><see cref="ReconciliationIntervalSeconds"/> as a timespan, floored at ten seconds.</summary>
    public TimeSpan ReconciliationInterval => TimeSpan.FromSeconds(Math.Max(10, ReconciliationIntervalSeconds));

    /// <summary><see cref="ReconciliationGraceMinutes"/> as a timespan, never longer than the timeout.</summary>
    public TimeSpan ReconciliationGrace =>
        TimeSpan.FromMinutes(Math.Clamp(ReconciliationGraceMinutes, 0, Math.Max(1, PendingPaymentTimeoutMinutes)));
}
