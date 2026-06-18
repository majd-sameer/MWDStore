using System.Globalization;
using Stripe;
using Stripe.Checkout;

namespace Store.Application.Payments.Stripe;

/// <summary>
/// Stripe SDK-backed <see cref="IStripeClient"/>. Uses the hosted Checkout Session flow: the shopper
/// is redirected to Stripe's secure page, pays (test card <c>4242 4242 4242 4242</c> in sandbox), and
/// returns to the storefront, where the session is read back to confirm payment. The secret key is
/// passed per call via <see cref="RequestOptions"/> rather than the global <c>StripeConfiguration</c>,
/// so each provider's key is used without shared mutable state.
/// </summary>
public sealed class StripeClient : IStripeClient
{
    /// <summary>Stripe events that carry a settled Checkout Session.</summary>
    private static readonly HashSet<string> SettleEvents =
    [
        EventTypes.CheckoutSessionCompleted,
        EventTypes.CheckoutSessionAsyncPaymentSucceeded
    ];

    public async Task<StripeSession> CreateCheckoutSessionAsync(
        StripeCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, string>
        {
            ["orderId"] = request.OrderId.ToString(CultureInfo.InvariantCulture),
            ["paymentId"] = request.PaymentId.ToString(CultureInfo.InvariantCulture)
        };

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = request.OrderId.ToString(CultureInfo.InvariantCulture),
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = ToMinorUnits(request.Amount, request.Currency),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.Description
                        }
                    }
                }
            ]
        };

        var service = new SessionService();
        var session = await service.CreateAsync(
            options, new RequestOptions { ApiKey = request.SecretKey }, cancellationToken);

        return ToSession(session);
    }

    public async Task<StripeSession> GetCheckoutSessionAsync(
        string sessionId, string secretKey, CancellationToken cancellationToken = default)
    {
        var service = new SessionService();
        var session = await service.GetAsync(
            sessionId, options: null, new RequestOptions { ApiKey = secretKey }, cancellationToken);

        return ToSession(session);
    }

    public StripeSession? ReadCheckoutSessionFromWebhook(string payload, string signatureHeader, string webhookSecret)
    {
        // Throws StripeException on a bad/forged signature — the controller maps that to 400.
        var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret);

        if (!SettleEvents.Contains(stripeEvent.Type) || stripeEvent.Data.Object is not Session session)
        {
            return null;
        }

        return ToSession(session);
    }

    private static StripeSession ToSession(Session session)
    {
        var paid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(session.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase);

        long orderId = 0;
        if (session.Metadata != null &&
            session.Metadata.TryGetValue("orderId", out var raw) &&
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            orderId = parsed;
        }

        return new StripeSession(session.Id, session.Url, paid, session.PaymentIntentId, orderId);
    }

    /// <summary>
    /// Converts a major-unit amount to Stripe's smallest currency unit. Honours zero-decimal
    /// currencies (no minor unit) and three-decimal currencies (JOD/KWD/BHD/…), which Stripe
    /// requires to be a multiple of 10; rounding the money to 2 decimals before scaling guarantees that.
    /// </summary>
    internal static long ToMinorUnits(decimal amount, string currency)
    {
        var code = (currency ?? string.Empty).ToLowerInvariant();

        if (ZeroDecimalCurrencies.Contains(code))
        {
            return (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        }

        if (ThreeDecimalCurrencies.Contains(code))
        {
            var twoDp = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            return (long)Math.Round(twoDp * 1000m, 0, MidpointRounding.AwayFromZero);
        }

        return (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
    }

    private static readonly HashSet<string> ZeroDecimalCurrencies =
    [
        "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga",
        "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
    ];

    private static readonly HashSet<string> ThreeDecimalCurrencies =
    [
        "bhd", "jod", "kwd", "omr", "tnd"
    ];
}
