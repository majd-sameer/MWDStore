namespace Store.Application.Payments.PayTabs;

/// <summary>
/// Thin port over the two PayTabs endpoints the Hosted Payment Page flow needs: create a page, and
/// ask what actually happened. Credentials are passed per call (like <c>IStripeClient</c>) so the
/// provider's saved keys are used without any shared mutable state.
/// </summary>
public interface IPayTabsClient
{
    /// <summary>
    /// <c>POST {region}/payment/request</c> — registers the transaction and returns the hosted page
    /// to redirect the shopper to.
    /// </summary>
    /// <exception cref="PayTabsException">The gateway rejected the request or returned no redirect URL.</exception>
    Task<PayTabsPage> CreateHostedPageAsync(PayTabsPageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST {region}/payment/query</c> — the authoritative status of a transaction. Always used to
    /// settle, so neither the browser redirect nor the callback body is ever trusted on its own.
    /// </summary>
    /// <exception cref="PayTabsException">The gateway rejected the query or returned no result.</exception>
    Task<PayTabsTransaction> QueryTransactionAsync(
        string baseUrl, string profileId, string serverKey, string tranRef, CancellationToken cancellationToken = default);
}
