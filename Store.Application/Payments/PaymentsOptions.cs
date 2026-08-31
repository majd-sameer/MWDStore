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
}
