using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Application.Payments;
using Store.Application.Payments.PayTabs;
using Store.Application.Payments.Stripe;
using Store.Data;

namespace Store.Api.Controllers;

/// <summary>
/// Customer-facing payments: the list of enabled payment methods the storefront checkout offers, plus
/// the shared redirect-gateway flow for Stripe / PayPal Express / MEPS — <c>initiate</c> starts a
/// payment for an order and returns where to send the shopper; <c>callback</c> is hit by the gateway
/// (or, in sandbox, by the storefront mock page) to settle it.
/// </summary>
/// <remarks>
/// Stub: in sandbox mode the signature check is skipped, so you can settle a payment by POSTing to
/// <c>callback</c> with <c>{ "orderId": N, "method": "Stripe", "result": "APPROVED", "gatewayTransactionId": "..." }</c>.
/// </remarks>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    /// <summary>Provider id of the Stripe gateway (matches the seeded <c>PaymentProvider</c> row).</summary>
    private const string StripeProviderId = "Stripe";

    /// <summary>Provider id of the MadfoatCom gateway, which runs on PayTabs' Hosted Payment Page.</summary>
    private const string MadfoatComProviderId = "MadfoatCom";

    private readonly IGatewayPaymentService _gateway;
    private readonly IStripeClient _stripe;
    private readonly StoreDbContext _db;
    private readonly PaymentsOptions _paymentsOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IGatewayPaymentService gateway,
        IStripeClient stripe,
        StoreDbContext db,
        PaymentsOptions paymentsOptions,
        ILogger<PaymentsController> logger)
    {
        _gateway = gateway;
        _stripe = stripe;
        _db = db;
        _paymentsOptions = paymentsOptions;
        _logger = logger;
    }

    /// <summary>
    /// The enabled payment methods the checkout should offer. Only the public id and display name are
    /// returned — gateway credentials in <c>AdditionalSettings</c> are never exposed to the storefront.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("methods")]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> Methods(CancellationToken cancellationToken)
    {
        var methods = await _db.PaymentProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .Select(p => new PaymentMethodDto(p.Id, p.Name))
            .ToListAsync(cancellationToken);

        return Ok(methods);
    }

    /// <summary>Starts a redirect-gateway payment for the signed-in customer's order.</summary>
    [Authorize]
    [HttpPost("initiate")]
    public async Task<ActionResult<GatewayInitiationResult>> Initiate(
        PaymentInitiateRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var result = await _gateway.InitiatePaymentAsync(
            request.Method, request.OrderId, customerId, request.ReturnUrl,
            RequestCulture.OverlayCultureId(Request), cancellationToken);

        return result.Success
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Starts a redirect-gateway payment for a guest order. Anonymous: the order's stored
    /// <c>GuestEmail</c> must match the supplied email (the same shared secret the track lookup uses)
    /// before the payment is initiated against the shared guest account.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("guest/initiate")]
    public async Task<ActionResult<GatewayInitiationResult>> GuestInitiate(
        GuestPaymentInitiateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email is required." });
        }

        var order = await _db.Orders
            .Where(o => o.Id == request.OrderId && o.ParentId == null)
            .Select(o => new { o.CustomerId, o.GuestEmail })
            .FirstOrDefaultAsync(cancellationToken);

        if (order?.GuestEmail == null ||
            !string.Equals(order.GuestEmail, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Order not found." });
        }

        var result = await _gateway.InitiatePaymentAsync(
            request.Method, request.OrderId, order.CustomerId, request.ReturnUrl,
            RequestCulture.OverlayCultureId(Request), cancellationToken);

        return result.Success
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Gateway callback / return URL. Anonymous: the request is authenticated by the signature
    /// the gateway includes (verified inside the service for live payments).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("callback")]
    public async Task<ActionResult<GatewayPaymentResult>> Callback(
        GatewayCallback callback, CancellationToken cancellationToken)
    {
        var result = await _gateway.HandleCallbackAsync(callback, cancellationToken);

        return result.Success
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Settles a Stripe Checkout payment from the storefront return page. Anonymous: the Stripe session
    /// id (unguessable, issued by Stripe and stored on the pending payment at initiation) authenticates
    /// the request. Idempotent — safe to call alongside the webhook.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("stripe/verify")]
    public async Task<ActionResult<GatewayPaymentResult>> StripeVerify(
        StripeVerifyRequest request, CancellationToken cancellationToken)
    {
        var result = await _gateway.SettleStripeSessionAsync(request.SessionId, cancellationToken);

        return result.Success
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Stripe webhook endpoint (production settlement path). Anonymous and authenticated by the Stripe
    /// signature over the raw body, verified against the provider's configured webhook secret. Returns
    /// 200 for events it doesn't act on so Stripe stops retrying; 400 only on a bad signature.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        var provider = await _db.PaymentProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == StripeProviderId && p.IsEnabled, cancellationToken);

        var webhookSecret = GatewaySettings.Parse(provider?.AdditionalSettings).StripeWebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            // Webhooks aren't configured (the storefront return page settles instead). Acknowledge so
            // Stripe doesn't keep retrying.
            return Ok();
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        StripeSession? session;
        try
        {
            session = _stripe.ReadCheckoutSessionFromWebhook(payload, signature, webhookSecret);
        }
        catch (global::Stripe.StripeException ex)
        {
            _logger.LogWarning(ex, "Rejected Stripe webhook with an invalid signature.");
            return BadRequest();
        }

        if (session != null)
        {
            await _gateway.SettleStripeSessionAsync(session.Id, cancellationToken);
        }

        return Ok();
    }

    // ---------------------------------------------------------------------
    // MadfoatCom / PayTabs Hosted Payment Page
    //
    // Three legs, deliberately split by trust level:
    //   return   — the shopper's browser comes back. Presentation only; changes nothing.
    //   verify   — the storefront asks us to settle, and we ask PayTabs what really happened.
    //   callback — PayTabs' server-to-server IPN, the safety net for shoppers who close the tab.
    // Both settlement paths go through SettlePayTabsTransactionAsync, which re-queries PayTabs, so
    // neither a forged return nor a replayed callback can mark an order paid on its own.
    // ---------------------------------------------------------------------

    /// <summary>
    /// PayTabs <c>return</c> target: the hosted page form-POSTs the shopper's browser here when they
    /// finish. An SPA route can't receive a cross-site POST, so this lands on the API and 302s on to
    /// the storefront, which then settles via <c>paytabs/verify</c>. Anonymous, and deliberately
    /// side-effect free — the signature is checked only to log tampering, never to authorize anything.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("paytabs/return")]
    [HttpGet("paytabs/return")]
    public async Task<IActionResult> PayTabsReturn(
        [FromQuery] long orderId = 0,
        [FromQuery] string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var fields = await ReadPayTabsReturnFieldsAsync(cancellationToken);

        // PayTabs uses camelCase on the return POST and snake_case in the API/IPN JSON.
        var tranRef = Value(fields, "tranRef") ?? Value(fields, "tran_ref");

        var settings = await LoadPayTabsSettingsAsync(cancellationToken);
        if (settings != null && settings.HasPayTabsKeys && fields.Count > 0)
        {
            var signature = Value(fields, PayTabsSignature.FieldName);
            if (!PayTabsSignature.VerifyReturn(fields!, signature, settings.PayTabsServerKey))
            {
                // Not fatal: settlement re-queries PayTabs anyway, so a forged return buys nothing.
                // Worth a warning because a genuine mismatch means a misconfigured server key.
                _logger.LogWarning(
                    "PayTabs return for order {OrderId} had an invalid signature; settling via query anyway.",
                    orderId);
            }
        }

        var storefront = _paymentsOptions.StorefrontBaseUrl.TrimEnd('/');
        var destination = string.IsNullOrWhiteSpace(returnUrl) ? "/account" : returnUrl;
        var target =
            $"{storefront}/payment/madfoatcom/return" +
            $"?tranRef={Uri.EscapeDataString(tranRef ?? string.Empty)}" +
            $"&orderId={orderId}" +
            $"&returnUrl={Uri.EscapeDataString(destination)}";

        return Redirect(target);
    }

    /// <summary>
    /// Settles a MadfoatCom payment from its PayTabs transaction reference, called by the storefront
    /// return page. Anonymous: the <c>tran_ref</c> (issued by PayTabs, stored on the pending payment at
    /// initiation) authenticates the request. Idempotent — safe to race the IPN.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("paytabs/verify")]
    public async Task<ActionResult<GatewayPaymentResult>> PayTabsVerify(
        PayTabsVerifyRequest request, CancellationToken cancellationToken)
    {
        var result = await _gateway.SettlePayTabsTransactionAsync(request.TranRef, cancellationToken);

        return result.Success
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// PayTabs <c>callback</c> (IPN) endpoint — the production settlement path. Anonymous and
    /// authenticated by the HMAC-SHA256 <c>signature</c> header over the raw body, verified against the
    /// provider's server key. Returns 200 for anything it doesn't act on so PayTabs stops retrying;
    /// 400 only on a bad signature.
    /// </summary>
    /// <remarks>
    /// This never fires against a localhost API — PayTabs' servers can't reach a private address — so
    /// in development the return page's verify call is what settles. Set <c>Payments:PublicApiBaseUrl</c>
    /// to a publicly reachable origin to enable it.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("paytabs/callback")]
    public async Task<IActionResult> PayTabsCallback(CancellationToken cancellationToken)
    {
        var settings = await LoadPayTabsSettingsAsync(cancellationToken);
        if (settings == null || !settings.HasPayTabsKeys)
        {
            // Not configured (or disabled) — acknowledge so PayTabs doesn't keep retrying.
            return Ok();
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers[PayTabsSignature.FieldName].ToString();

        if (!PayTabsSignature.VerifyCallback(payload, signature, settings.PayTabsServerKey))
        {
            _logger.LogWarning("Rejected a PayTabs callback with an invalid signature.");
            return BadRequest();
        }

        var tranRef = ReadTranRef(payload);
        if (string.IsNullOrWhiteSpace(tranRef))
        {
            _logger.LogWarning("Ignored a signed PayTabs callback that carried no tran_ref.");
            return Ok();
        }

        // The body is signed, but settlement still re-queries PayTabs for the status — a valid
        // signature proves origin, not that the payload is the latest word on the transaction.
        var result = await _gateway.SettlePayTabsTransactionAsync(tranRef, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning(
                "PayTabs callback for {TranRef} could not be settled: {Error}", tranRef, result.Error);
        }

        return Ok();
    }

    /// <summary>Query parameters this API adds to the return URL — never part of what PayTabs signed.</summary>
    private static readonly string[] OurReturnParameters = ["orderId", "returnUrl"];

    /// <summary>
    /// The fields PayTabs signed, whichever way the return arrives — a form POST normally, a query
    /// string if the profile is configured to redirect with GET.
    /// </summary>
    /// <remarks>
    /// Only PayTabs' own fields may go into this set. The <c>orderId</c> and <c>returnUrl</c> we
    /// appended to the return URL travel back on the query string, and folding them in would add
    /// entries PayTabs never hashed — making every legitimate signature fail verification, which in
    /// turn buries a real tampering warning in noise. So a form POST is taken as the payload in full,
    /// and the query string is consulted only when there is no form (the GET variant), minus our own
    /// parameters.
    /// </remarks>
    private async Task<Dictionary<string, string?>> ReadPayTabsReturnFieldsAsync(CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            foreach (var entry in form)
            {
                fields[entry.Key] = entry.Value.ToString();
            }

            return fields;
        }

        foreach (var entry in Request.Query)
        {
            if (!OurReturnParameters.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                fields[entry.Key] = entry.Value.ToString();
            }
        }

        return fields;
    }

    /// <summary>The MadfoatCom provider's parsed settings, or null when it's missing or disabled.</summary>
    private async Task<GatewaySettings?> LoadPayTabsSettingsAsync(CancellationToken cancellationToken)
    {
        var provider = await _db.PaymentProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == MadfoatComProviderId && p.IsEnabled, cancellationToken);

        return provider == null ? null : GatewaySettings.Parse(provider.AdditionalSettings);
    }

    /// <summary>Pulls <c>tran_ref</c> out of an IPN body without modelling the whole transaction.</summary>
    private static string? ReadTranRef(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("tran_ref", out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Value(Dictionary<string, string?> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

/// <summary>A payment method offered at checkout (no gateway credentials).</summary>
/// <param name="Id">Provider id (e.g. <c>CoD</c>, <c>Stripe</c>, <c>MEPS</c>) — sent back as the order's payment method.</param>
/// <param name="Name">Display name shown to the shopper.</param>
public sealed record PaymentMethodDto(string Id, string Name);

/// <summary>Request to start a redirect-gateway payment.</summary>
/// <param name="OrderId">The order to pay (must belong to the signed-in customer).</param>
/// <param name="Method">Provider id handling the payment (e.g. <c>Stripe</c>, <c>PaypalExpress</c>, <c>MEPS</c>).</param>
/// <param name="ReturnUrl">Where the gateway should send the shopper back after paying.</param>
public sealed record PaymentInitiateRequest(long OrderId, string Method, string ReturnUrl);

/// <summary>Request to settle a Stripe Checkout payment from its session id.</summary>
/// <param name="SessionId">The Stripe Checkout Session id (<c>cs_test_…</c>) returned on the success URL.</param>
public sealed record StripeVerifyRequest(string SessionId);

/// <summary>Request to settle a MadfoatCom payment from its PayTabs transaction reference.</summary>
/// <param name="TranRef">The PayTabs <c>tran_ref</c> (<c>TST…</c>) carried back on the return.</param>
public sealed record PayTabsVerifyRequest(string TranRef);

/// <summary>Request to start a redirect-gateway payment for a guest order (validated by <paramref name="Email"/>).</summary>
/// <param name="OrderId">The guest order to pay.</param>
/// <param name="Method">Provider id handling the payment (e.g. <c>Stripe</c>, <c>PaypalExpress</c>, <c>MEPS</c>).</param>
/// <param name="ReturnUrl">Where the gateway should send the shopper back after paying.</param>
/// <param name="Email">The email the order was placed under (must match the order's <c>GuestEmail</c>).</param>
public sealed record GuestPaymentInitiateRequest(long OrderId, string Method, string ReturnUrl, string Email);
