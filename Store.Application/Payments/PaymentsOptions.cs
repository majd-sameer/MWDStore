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
}
