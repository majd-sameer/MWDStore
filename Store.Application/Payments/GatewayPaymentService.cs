using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;
using Store.Application.Orders;
using Store.Application.Payments.Stripe;
using Store.Data;
using Store.Domain;

namespace Store.Application.Payments;

/// <summary>
/// Stub processor shared by the redirect gateways (Stripe, PayPal Express, MEPS). Wires the full
/// two-leg flow (initiate → pending payment → callback → settle) against the real
/// <c>Payment</c>/<c>Order</c> tables, but the actual gateway HTTP calls are not implemented: the
/// sandbox path simulates an approval so the flow can be exercised end-to-end. Search for
/// <c>TODO(payments)</c> for the spots that need each gateway's live spec.
/// </summary>
public sealed class GatewayPaymentService : IGatewayPaymentService
{
    /// <summary>Cash on delivery is settled offline — it never goes through this gateway flow.</summary>
    private const string CashOnDelivery = "CoD";

    /// <summary>Provider id of the Stripe gateway (matches the seeded <c>PaymentProvider</c> row).</summary>
    private const string Stripe = "Stripe";

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IStripeClient _stripe;
    private readonly PaymentsOptions _options;
    private readonly ILogger<GatewayPaymentService> _logger;

    public GatewayPaymentService(
        StoreDbContext db,
        TimeProvider timeProvider,
        IStripeClient stripe,
        PaymentsOptions options,
        ILogger<GatewayPaymentService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _stripe = stripe;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<GatewayInitiationResult>> InitiatePaymentAsync(
        string method, long orderId, long customerId, string returnUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(method) || string.Equals(method, CashOnDelivery, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<GatewayInitiationResult>("This payment method does not require online payment.");
        }

        var order = await _db.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null || order.CustomerId != customerId)
        {
            return Result.Fail<GatewayInitiationResult>("Order not found.");
        }

        if (order.OrderStatus is not (OrderStatus.New or OrderStatus.PendingPayment))
        {
            return Result.Fail<GatewayInitiationResult>("This order can no longer be paid.");
        }

        var settingsResult = await LoadEnabledSettingsAsync(method, cancellationToken);
        if (!settingsResult.Success)
        {
            return Result.Fail<GatewayInitiationResult>(settingsResult.Error!);
        }

        var settings = settingsResult.Value!;
        if (!settings.IsSandbox && !settings.HasCredentials)
        {
            return Result.Fail<GatewayInitiationResult>($"{method} is not configured for live payments.");
        }

        var now = _timeProvider.GetUtcNow();
        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.OrderTotal,
            PaymentFee = settings.PaymentFee,
            PaymentMethod = method,
            Status = PaymentStatus.PendingExecution,
            CreatedOn = now,
            LatestUpdatedOn = now
        };
        _db.Payments.Add(payment);

        SetOrderStatus(order, OrderStatus.PendingPayment, now, $"{method} payment initiated.");
        await _db.SaveChangesAsync(cancellationToken);

        // Stripe (when real keys are configured) runs a live Checkout Session against Stripe's API —
        // even with sandbox `sk_test_…` keys this is a genuine redirect to Stripe's hosted page, not the
        // local mock. IsSandbox=false tells the storefront to redirect to `RedirectUrl` rather than show
        // the simulated gateway page.
        if (string.Equals(method, Stripe, StringComparison.OrdinalIgnoreCase) && settings.HasStripeKeys)
        {
            return await CreateStripeCheckoutAsync(order, payment, settings, returnUrl, cancellationToken);
        }

        // TODO(payments): call the gateway "create session / register order" API with a signed request
        // and use the hosted-payment-page URL it returns instead of composing one by hand. In sandbox
        // the storefront ignores this URL and shows a local mock gateway page.
        var redirectUrl = BuildHostedPageUrl(method, settings, order, payment, returnUrl);

        return Result.Ok(new GatewayInitiationResult(payment.Id, order.Id, method, redirectUrl, settings.IsSandbox));
    }

    public async Task<Result<GatewayPaymentResult>> HandleCallbackAsync(
        GatewayCallback callback, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == callback.OrderId, cancellationToken);

        if (order == null)
        {
            return Result.Fail<GatewayPaymentResult>("Order not found.");
        }

        var payment = order.Payments
            .Where(p => p.PaymentMethod == callback.Method)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault(p => p.Status == PaymentStatus.PendingExecution);

        if (payment == null)
        {
            return Result.Fail<GatewayPaymentResult>("No pending payment for this order.");
        }

        var settingsResult = await LoadEnabledSettingsAsync(callback.Method, cancellationToken);
        if (!settingsResult.Success)
        {
            return Result.Fail<GatewayPaymentResult>(settingsResult.Error!);
        }

        var settings = settingsResult.Value!;

        // TODO(payments): production callbacks must be authenticated. Verify the gateway signature
        // (and ideally re-query the gateway for the authoritative status) before trusting the result.
        if (!settings.IsSandbox && !VerifySignature(settings, callback))
        {
            return Result.Fail<GatewayPaymentResult>("Invalid payment callback signature.");
        }

        var approved = IsApproved(callback.Result);
        var now = _timeProvider.GetUtcNow();

        payment.Status = approved ? PaymentStatus.Succeeded : PaymentStatus.Failed;
        payment.GatewayTransactionId = callback.GatewayTransactionId;
        payment.FailureMessage = approved ? null : (callback.Result ?? "Payment declined.");
        payment.LatestUpdatedOn = now;

        SetOrderStatus(
            order,
            approved ? OrderStatus.PaymentReceived : OrderStatus.PaymentFailed,
            now,
            approved ? $"{callback.Method} payment received." : $"{callback.Method} payment failed.");

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(new GatewayPaymentResult(payment.Id, order.Id, approved, callback.GatewayTransactionId));
    }

    public async Task<Result<GatewayPaymentResult>> SettleStripeSessionAsync(
        string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result.Fail<GatewayPaymentResult>("Missing Stripe session.");
        }

        // The session id was stored on the payment at initiation, so it both locates the payment and
        // proves the caller initiated it (Stripe session ids are unguessable).
        var payment = await _db.Payments
            .Include(p => p.Order)
            .ThenInclude(o => o.OrderHistories)
            .Where(p => p.PaymentMethod == Stripe && p.GatewayTransactionId == sessionId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment == null)
        {
            return Result.Fail<GatewayPaymentResult>("Payment not found for this session.");
        }

        var order = payment.Order;

        // Idempotent: the return page and the webhook can both settle the same session.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            return Result.Ok(new GatewayPaymentResult(payment.Id, order.Id, true, payment.GatewayTransactionId));
        }

        var settingsResult = await LoadEnabledSettingsAsync(Stripe, cancellationToken);
        if (!settingsResult.Success)
        {
            return Result.Fail<GatewayPaymentResult>(settingsResult.Error!);
        }

        StripeSession session;
        try
        {
            session = await _stripe.GetCheckoutSessionAsync(
                sessionId, settingsResult.Value!.StripeSecretKey, cancellationToken);
        }
        catch (global::Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Failed to retrieve Stripe session {SessionId} for order {OrderId}.", sessionId, order.Id);
            return Result.Fail<GatewayPaymentResult>("Could not verify the payment with Stripe.");
        }

        var now = _timeProvider.GetUtcNow();
        if (session.IsPaid)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.FailureMessage = null;
            payment.LatestUpdatedOn = now;
            SetOrderStatus(order, OrderStatus.PaymentReceived, now, "Stripe payment received.");
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Ok(new GatewayPaymentResult(payment.Id, order.Id, true, sessionId));
        }

        // Not paid yet (abandoned / canceled / still processing). Leave the order PendingPayment so the
        // shopper can retry; only flag the payment row as failed for this attempt.
        payment.Status = PaymentStatus.Failed;
        payment.FailureMessage = "Stripe payment was not completed.";
        payment.LatestUpdatedOn = now;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok(new GatewayPaymentResult(payment.Id, order.Id, false, sessionId));
    }

    private async Task<Result<GatewayInitiationResult>> CreateStripeCheckoutAsync(
        Order order, Payment payment, GatewaySettings settings, string returnUrl, CancellationToken cancellationToken)
    {
        var baseUrl = _options.StorefrontBaseUrl.TrimEnd('/');
        var returnArg = Uri.EscapeDataString(string.IsNullOrWhiteSpace(returnUrl) ? "/account" : returnUrl);
        // Stripe substitutes the literal {CHECKOUT_SESSION_ID} placeholder when redirecting back.
        var successUrl =
            $"{baseUrl}/payment/stripe/return?orderId={order.Id}&returnUrl={returnArg}&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl =
            $"{baseUrl}/payment/stripe/return?orderId={order.Id}&returnUrl={returnArg}&canceled=1";

        try
        {
            var session = await _stripe.CreateCheckoutSessionAsync(
                new StripeCheckoutRequest(
                    OrderId: order.Id,
                    PaymentId: payment.Id,
                    Amount: payment.Amount,
                    Currency: settings.Currency,
                    Description: $"Order #{order.Id}",
                    CustomerEmail: order.GuestEmail,
                    SecretKey: settings.StripeSecretKey,
                    SuccessUrl: successUrl,
                    CancelUrl: cancelUrl),
                cancellationToken);

            // Persist the session id so the return/webhook can locate and settle this payment.
            payment.GatewayTransactionId = session.Id;
            payment.LatestUpdatedOn = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            return Result.Ok(new GatewayInitiationResult(
                payment.Id, order.Id, Stripe, session.Url ?? string.Empty, IsSandbox: false));
        }
        catch (global::Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe Checkout session creation failed for order {OrderId}.", order.Id);
            return Result.Fail<GatewayInitiationResult>(
                "Could not start the Stripe payment. Check the gateway keys and currency.");
        }
    }

    private async Task<Result<GatewaySettings>> LoadEnabledSettingsAsync(string method, CancellationToken cancellationToken)
    {
        var provider = await _db.PaymentProviders
            .FirstOrDefaultAsync(p => p.Id == method, cancellationToken);

        if (provider == null || !provider.IsEnabled)
        {
            return Result.Fail<GatewaySettings>($"{method} payments are not enabled.");
        }

        return Result.Ok(GatewaySettings.Parse(provider.AdditionalSettings));
    }

    private void SetOrderStatus(Order order, int newStatus, DateTimeOffset now, string note)
    {
        var oldStatus = order.OrderStatus;
        order.OrderStatus = newStatus;
        order.LatestUpdatedOn = now;
        order.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = note,
            CreatedOn = now,
            CreatedById = order.CustomerId
        });
    }

    /// <summary>
    /// Composes a hosted-payment-page redirect with a signed request. The exact parameter names and
    /// signing scheme are placeholders — replace per gateway. TODO(payments).
    /// </summary>
    private static string BuildHostedPageUrl(
        string method, GatewaySettings settings, Order order, Payment payment, string returnUrl)
    {
        var amount = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var signature = Sign(settings, order.Id, payment.Id, amount);

        var query = new[]
        {
            $"method={Uri.EscapeDataString(method)}",
            $"orderRef={order.Id}",
            $"paymentRef={payment.Id}",
            $"amount={amount}",
            $"currency=JOD",
            $"returnUrl={Uri.EscapeDataString(returnUrl)}",
            $"signature={signature}"
        };

        return $"https://sandbox.gateway.local/{Uri.EscapeDataString(method)}/hpp?{string.Join('&', query)}";
    }

    private static bool VerifySignature(GatewaySettings settings, GatewayCallback callback)
    {
        if (string.IsNullOrEmpty(callback.Signature))
        {
            return false;
        }

        // TODO(payments): build the canonical string from the exact fields/order each gateway signs.
        var expected = Sign(settings, callback.OrderId, 0, callback.Result ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(callback.Signature));
    }

    private static string Sign(GatewaySettings settings, long orderId, long paymentId, string trailing)
    {
        var canonical = $"{orderId}|{paymentId}|{trailing}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(settings.SigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }

    private static bool IsApproved(string? result) =>
        string.Equals(result, "APPROVED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(result, "SUCCESS", StringComparison.OrdinalIgnoreCase);
}
