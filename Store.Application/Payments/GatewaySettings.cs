using System.Text.Json;

namespace Store.Application.Payments;

/// <summary>
/// Generic, casing-tolerant view of a payment provider's <c>AdditionalSettings</c> JSON blob
/// (edited in the admin per-gateway config forms). Each gateway stores different fields
/// (Stripe: public/private keys; PayPal: client id/secret; MEPS: merchant/terminal/secret), so
/// this exposes only what the stub processor needs: sandbox flag, fee, and whether any
/// credentials are present plus a best-effort signing secret.
/// </summary>
/// <remarks>
/// This is a stub: the sandbox path simulates an approval so the flow is testable end-to-end
/// without a live gateway account. Real per-gateway request/signing belongs behind TODO(payments).
/// </remarks>
public sealed record GatewaySettings
{
    /// <summary>Credential keys any of the standard providers may store.</summary>
    private static readonly string[] CredentialKeys =
        ["clientId", "clientSecret", "secretKey", "privateKey", "publicKey", "merchantId", "terminalId"];

    /// <summary>Keys (in priority order) used as the HMAC signing secret for callbacks.</summary>
    private static readonly string[] SecretKeys = ["secretKey", "clientSecret", "privateKey"];

    public bool IsSandbox { get; init; } = true;

    public decimal PaymentFee { get; init; }

    /// <summary>True when the provider has at least one non-empty credential configured.</summary>
    public bool HasCredentials { get; init; }

    /// <summary>Best-effort secret for signing/verifying callbacks (empty when none configured).</summary>
    public string SigningSecret { get; init; } = string.Empty;

    /// <summary>Stripe publishable key (<c>pk_test_…</c>/<c>pk_live_…</c>); empty for non-Stripe providers.</summary>
    public string StripePublishableKey { get; init; } = string.Empty;

    /// <summary>Stripe secret key (<c>sk_test_…</c>/<c>sk_live_…</c>) used for server-side API calls.</summary>
    public string StripeSecretKey { get; init; } = string.Empty;

    /// <summary>Stripe webhook signing secret (<c>whsec_…</c>); empty when webhooks aren't configured.</summary>
    public string StripeWebhookSecret { get; init; } = string.Empty;

    /// <summary>
    /// ISO currency the gateway charges in (lower-case, e.g. <c>jod</c>). Defaults to JOD — the
    /// store's currency. Configurable per provider because a Stripe account may only have certain
    /// presentment currencies enabled in test mode.
    /// </summary>
    public string Currency { get; init; } = "jod";

    /// <summary>True when both Stripe keys are present, so the live Stripe Checkout flow can run.</summary>
    public bool HasStripeKeys =>
        !string.IsNullOrWhiteSpace(StripePublishableKey) && !string.IsNullOrWhiteSpace(StripeSecretKey);

    /// <summary>Parses the provider's settings JSON, tolerant of camel/Pascal casing.</summary>
    public static GatewaySettings Parse(string? additionalSettings)
    {
        if (string.IsNullOrWhiteSpace(additionalSettings))
        {
            return new GatewaySettings();
        }

        try
        {
            using var doc = JsonDocument.Parse(additionalSettings);
            var root = doc.RootElement;
            var currency = FirstNonEmpty(root, ["currency", "Currency"]);
            return new GatewaySettings
            {
                IsSandbox = GetBool(root, "isSandbox", "IsSandbox", defaultValue: true),
                PaymentFee = GetDecimal(root, "paymentFee", "PaymentFee"),
                HasCredentials = CredentialKeys.Any(k => !string.IsNullOrWhiteSpace(GetString(root, k))),
                SigningSecret = FirstNonEmpty(root, SecretKeys),
                StripePublishableKey = FirstNonEmpty(root, ["publicKey", "PublicKey", "publishableKey", "PublishableKey"]),
                StripeSecretKey = FirstNonEmpty(root, ["privateKey", "PrivateKey", "secretKey", "SecretKey"]),
                StripeWebhookSecret = FirstNonEmpty(root, ["webhookSecret", "WebhookSecret"]),
                Currency = string.IsNullOrWhiteSpace(currency) ? "jod" : currency.Trim().ToLowerInvariant()
            };
        }
        catch (JsonException)
        {
            return new GatewaySettings();
        }
    }

    private static string FirstNonEmpty(JsonElement root, string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(root, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string GetString(JsonElement root, string name)
    {
        foreach (var candidate in new[] { name, Pascal(name) })
        {
            if (root.TryGetProperty(candidate, out var el) && el.ValueKind == JsonValueKind.String)
            {
                return el.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool GetBool(JsonElement root, string name, string altName, bool defaultValue)
    {
        foreach (var n in new[] { name, altName })
        {
            if (root.TryGetProperty(n, out var el) &&
                (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
            {
                return el.GetBoolean();
            }
        }

        return defaultValue;
    }

    private static decimal GetDecimal(JsonElement root, string name, string altName)
    {
        foreach (var n in new[] { name, altName })
        {
            if (root.TryGetProperty(n, out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
                {
                    return d;
                }

                if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out var ds))
                {
                    return ds;
                }
            }
        }

        return 0m;
    }

    private static string Pascal(string camel) =>
        string.IsNullOrEmpty(camel) ? camel : char.ToUpperInvariant(camel[0]) + camel[1..];
}
