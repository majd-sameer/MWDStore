using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Application.Payments;
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

    private readonly IGatewayPaymentService _gateway;
    private readonly IStripeClient _stripe;
    private readonly StoreDbContext _db;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IGatewayPaymentService gateway, IStripeClient stripe, StoreDbContext db, ILogger<PaymentsController> logger)
    {
        _gateway = gateway;
        _stripe = stripe;
        _db = db;
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
            request.Method, request.OrderId, customerId, request.ReturnUrl, cancellationToken);

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
            request.Method, request.OrderId, order.CustomerId, request.ReturnUrl, cancellationToken);

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

/// <summary>Request to start a redirect-gateway payment for a guest order (validated by <paramref name="Email"/>).</summary>
/// <param name="OrderId">The guest order to pay.</param>
/// <param name="Method">Provider id handling the payment (e.g. <c>Stripe</c>, <c>PaypalExpress</c>, <c>MEPS</c>).</param>
/// <param name="ReturnUrl">Where the gateway should send the shopper back after paying.</param>
/// <param name="Email">The email the order was placed under (must match the order's <c>GuestEmail</c>).</param>
public sealed record GuestPaymentInitiateRequest(long OrderId, string Method, string ReturnUrl, string Email);
