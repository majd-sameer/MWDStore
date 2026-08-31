using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Store.Application.Payments.PayTabs;

/// <summary>
/// <see cref="IPayTabsClient"/> over PayTabs' JSON API.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton holding one long-lived <see cref="HttpClient"/>. That is the correct
/// shape for a client with a fixed set of endpoints — the anti-pattern is a client per request, not a
/// shared one — and it keeps <c>Store.Application</c> free of a <c>Microsoft.Extensions.Http</c>
/// dependency. <see cref="SocketsHttpHandler.PooledConnectionLifetime"/> is set so DNS changes are
/// still picked up, which is the one thing a singleton client would otherwise miss.
/// </para>
/// <para>
/// Authentication is the profile's server key sent verbatim in an <c>authorization</c> header — not a
/// <c>Bearer</c> token, which is why it is added without validation.
/// </para>
/// </remarks>
public sealed class PayTabsClient : IPayTabsClient
{
    /// <summary>Currencies with three minor digits; everything else PayTabs expects at two.</summary>
    private static readonly HashSet<string> ThreeDecimalCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND" };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<PayTabsPage> CreateHostedPageAsync(
        PayTabsPageRequest request, CancellationToken cancellationToken = default)
    {
        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();

        var body = new Dictionary<string, object?>
        {
            ["profile_id"] = ParseProfileId(request.ProfileId),
            ["tran_type"] = "sale",
            ["tran_class"] = "ecom",
            ["cart_id"] = request.CartId,
            ["cart_description"] = Truncate(request.CartDescription, 128),
            ["cart_currency"] = currency,
            ["cart_amount"] = RoundForCurrency(request.Amount, currency),
            ["paypage_lang"] = request.Language,
            ["return"] = request.ReturnUrl,
            // The shopper already gave us their address at checkout, so PayTabs only needs to collect
            // card details. Hiding shipping keeps the hosted page to a single step.
            ["hide_shipping"] = true,
            ["framed"] = false
        };

        if (!string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            body["callback"] = request.CallbackUrl;
        }

        AddParty(body, "customer_details", request.Customer);
        AddParty(body, "shipping_details", request.Shipping);

        var response = await PostAsync<CreatePageResponse>(
            request.BaseUrl, PayTabsRegions.RequestPath, request.ServerKey, body, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.RedirectUrl) || string.IsNullOrWhiteSpace(response.TranRef))
        {
            throw new PayTabsException(
                response.Message ?? "PayTabs did not return a payment page.", response.Code);
        }

        return new PayTabsPage(response.TranRef, response.RedirectUrl);
    }

    public async Task<PayTabsTransaction> QueryTransactionAsync(
        string baseUrl, string profileId, string serverKey, string tranRef, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["profile_id"] = ParseProfileId(profileId),
            ["tran_ref"] = tranRef
        };

        var response = await PostAsync<QueryResponse>(
            baseUrl, PayTabsRegions.QueryPath, serverKey, body, cancellationToken);

        if (response.PaymentResult == null)
        {
            throw new PayTabsException(
                response.Message ?? "PayTabs returned no result for this transaction.", response.Code);
        }

        return new PayTabsTransaction(
            response.TranRef ?? tranRef,
            response.CartId,
            response.PaymentResult.ResponseStatus ?? PayTabsResponseStatus.Pending,
            response.PaymentResult.ResponseCode,
            response.PaymentResult.ResponseMessage);
    }

    /// <summary>
    /// POSTs <paramref name="body"/> and deserializes the reply. PayTabs reports validation failures
    /// as a JSON <c>{ code, message }</c> — sometimes with a 4xx, sometimes with a 200 — so the body is
    /// always read and the status code alone is never the verdict.
    /// </summary>
    private static async Task<T> PostAsync<T>(
        string baseUrl,
        string path,
        string serverKey,
        Dictionary<string, object?> body,
        CancellationToken cancellationToken)
        where T : PayTabsResponseBase
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}{path}")
        {
            Content = JsonContent.Create(body, options: SerializerOptions)
        };

        // The server key is the whole credential — no scheme prefix — so it fails header validation.
        message.Headers.TryAddWithoutValidation("authorization", serverKey);

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new PayTabsException($"Could not reach PayTabs: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PayTabsException("PayTabs did not respond in time.");
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            T? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<T>(payload, SerializerOptions);
            }
            catch (JsonException)
            {
                // Falls through to the status-code check below — an HTML error page lands here.
            }

            if (parsed == null)
            {
                throw new PayTabsException(
                    $"PayTabs returned an unreadable {(int)response.StatusCode} response.");
            }

            // A non-success status always means failure; surface PayTabs' own message when present.
            if (!response.IsSuccessStatusCode)
            {
                throw new PayTabsException(
                    parsed.Message ?? $"PayTabs rejected the request ({(int)response.StatusCode}).", parsed.Code);
            }

            return parsed;
        }
    }

    /// <summary>Adds a <c>customer_details</c>-shaped object, skipping it entirely when empty.</summary>
    private static void AddParty(Dictionary<string, object?> body, string key, PayTabsParty? party)
    {
        if (party == null)
        {
            return;
        }

        var details = new Dictionary<string, object?>();
        Add(details, "name", party.Name);
        Add(details, "email", party.Email);
        Add(details, "phone", party.Phone);
        Add(details, "street1", party.Street1);
        Add(details, "city", party.City);
        Add(details, "state", party.State);
        Add(details, "country", party.Country);
        Add(details, "zip", party.Zip);

        if (details.Count > 0)
        {
            body[key] = details;
        }

        static void Add(Dictionary<string, object?> target, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[name] = value.Trim();
            }
        }
    }

    /// <summary>
    /// PayTabs types <c>profile_id</c> as an integer. Sending it as a string is one of the two usual
    /// causes of "Authentication failed" (the other being the wrong region), so it is coerced here.
    /// </summary>
    private static object ParseProfileId(string profileId) =>
        int.TryParse(profileId?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : profileId?.Trim() ?? string.Empty;

    /// <summary>Rounds to the currency's minor units — PayTabs rejects amounts with excess precision.</summary>
    internal static decimal RoundForCurrency(decimal amount, string currency) =>
        Math.Round(amount, ThreeDecimalCurrencies.Contains(currency) ? 3 : 2, MidpointRounding.AwayFromZero);

    private static string Truncate(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    /// <summary>Fields every PayTabs reply may carry, including the error shape.</summary>
    private abstract class PayTabsResponseBase
    {
        [JsonPropertyName("code")]
        public int? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed class CreatePageResponse : PayTabsResponseBase
    {
        [JsonPropertyName("tran_ref")]
        public string? TranRef { get; init; }

        [JsonPropertyName("redirect_url")]
        public string? RedirectUrl { get; init; }
    }

    private sealed class QueryResponse : PayTabsResponseBase
    {
        [JsonPropertyName("tran_ref")]
        public string? TranRef { get; init; }

        [JsonPropertyName("cart_id")]
        public string? CartId { get; init; }

        [JsonPropertyName("payment_result")]
        public PaymentResultDto? PaymentResult { get; init; }
    }

    private sealed class PaymentResultDto
    {
        [JsonPropertyName("response_status")]
        public string? ResponseStatus { get; init; }

        [JsonPropertyName("response_code")]
        [JsonConverter(typeof(LooseStringConverter))]
        public string? ResponseCode { get; init; }

        [JsonPropertyName("response_message")]
        public string? ResponseMessage { get; init; }
    }

    /// <summary>
    /// Reads a field that PayTabs sometimes quotes and sometimes doesn't — <c>response_code</c> comes
    /// back as <c>"G13319"</c> for cards but as a bare number for some acquirers. Without this the
    /// deserializer throws and a settled payment would look like a gateway failure.
    /// </summary>
    private sealed class LooseStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
                JsonTokenType.True or JsonTokenType.False => reader.GetBoolean().ToString(),
                JsonTokenType.Null => null,
                _ => null
            };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
