using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;
using Store.Application.Orders;
using Store.Application.Payments.PayTabs;
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

    /// <summary>
    /// Provider id of the MadfoatCom gateway, which runs on PayTabs' Hosted Payment Page (matches the
    /// seeded <c>PaymentProvider</c> row).
    /// </summary>
    public const string MadfoatCom = "MadfoatCom";

    /// <summary>Attempts one reconciliation pass looks at, so a backlog can't hold a transaction open.</summary>
    private const int ReconciliationBatchSize = 100;

    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IStripeClient _stripe;
    private readonly IPayTabsClient _payTabs;
    private readonly IOrderService _orders;
    private readonly PaymentsOptions _options;
    private readonly ILogger<GatewayPaymentService> _logger;

    public GatewayPaymentService(
        StoreDbContext db,
        TimeProvider timeProvider,
        IStripeClient stripe,
        IPayTabsClient payTabs,
        IOrderService orders,
        PaymentsOptions options,
        ILogger<GatewayPaymentService> logger)
    {
        _db = db;
        _timeProvider = timeProvider;
        _stripe = stripe;
        _payTabs = payTabs;
        _orders = orders;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<GatewayInitiationResult>> InitiatePaymentAsync(
        string method,
        long orderId,
        long customerId,
        string returnUrl,
        string? language = null,
        CancellationToken cancellationToken = default)
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

        // MadfoatCom is a real PayTabs integration, not a stub: it always registers the transaction
        // with PayTabs and redirects to the page PayTabs returns. A demo profile is still a live API
        // call — "sandbox" there means test cards, not a simulated flow — so IsSandbox=false is
        // returned regardless, telling the storefront to redirect rather than show the local mock.
        if (string.Equals(method, MadfoatCom, StringComparison.OrdinalIgnoreCase))
        {
            return await CreatePayTabsPageAsync(order, payment, settings, returnUrl, language, cancellationToken);
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

    public async Task<Result<GatewayPaymentResult>> SettlePayTabsTransactionAsync(
        string tranRef, CancellationToken cancellationToken = default) =>
        (await SettlePayTabsAsync(tranRef, cancellationToken)).Result;

    /// <summary>
    /// The settlement body. Also used by the reconciliation sweep, which additionally needs to know
    /// whether PayTabs actually <i>decided</i> the transaction: an attempt PayTabs still calls pending
    /// is the only kind the sweep may eventually void.
    /// </summary>
    private async Task<(Result<GatewayPaymentResult> Result, bool Decided)> SettlePayTabsAsync(
        string tranRef, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tranRef))
        {
            return (Result.Fail<GatewayPaymentResult>("Missing PayTabs transaction reference."), false);
        }

        // The tran_ref was stored on the payment at initiation, so it both locates the attempt and
        // proves the caller initiated it (PayTabs issues it and it is unguessable). Every row written
        // for the transaction is loaded — the attempt, plus any settlement row already recorded.
        var rows = await _db.Payments
            .Include(p => p.Order)
            .ThenInclude(o => o.OrderHistories)
            .Where(p => p.PaymentMethod == MadfoatCom && p.GatewayTransactionId == tranRef)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return (Result.Fail<GatewayPaymentResult>("Payment not found for this transaction."), false);
        }

        var attempt = rows.FirstOrDefault(p => p.Status == PaymentStatus.PendingExecution) ?? rows[0];
        var order = attempt.Order;

        // Idempotent: the return page, the IPN and the sweep routinely settle the same transaction at
        // once, and a recorded success is the final word — re-querying could only duplicate the row.
        var settled = rows.FirstOrDefault(p => p.Status == PaymentStatus.Succeeded);
        if (settled != null)
        {
            return (Result.Ok(new GatewayPaymentResult(settled.Id, order.Id, true, tranRef)), true);
        }

        var settingsResult = await LoadEnabledSettingsAsync(MadfoatCom, cancellationToken);
        if (!settingsResult.Success)
        {
            return (Result.Fail<GatewayPaymentResult>(settingsResult.Error!), false);
        }

        var settings = settingsResult.Value!;
        if (!settings.HasPayTabsKeys)
        {
            return (Result.Fail<GatewayPaymentResult>("MadfoatCom is not configured."), false);
        }

        PayTabsTransaction transaction;
        try
        {
            // Always re-query: the browser return is attacker-controllable and the IPN body is only as
            // trustworthy as its signature, so PayTabs' own answer is the only thing settlement acts on.
            transaction = await _payTabs.QueryTransactionAsync(
                settings.PayTabsBaseUrl,
                settings.PayTabsProfileId,
                settings.PayTabsServerKey,
                tranRef,
                cancellationToken);
        }
        catch (PayTabsException ex)
        {
            _logger.LogError(
                ex, "Failed to query PayTabs transaction {TranRef} for order {OrderId}.", tranRef, order.Id);
            return (Result.Fail<GatewayPaymentResult>("Could not verify the payment with MadfoatCom."), false);
        }

        var now = _timeProvider.GetUtcNow();

        if (transaction.IsApproved)
        {
            var payment = RecordSettlement(attempt, PaymentStatus.Succeeded, null, now);

            if (order.OrderStatus == OrderStatus.Canceled)
            {
                // The money landed after the timeout had already canceled the order and put the stock
                // back. Record it — it is real and refundable — but don't resurrect an order whose
                // stock is gone; this needs a human, so it is logged as an error and noted on the order.
                _logger.LogError(
                    "MadfoatCom transaction {TranRef} was approved after order {OrderId} had been canceled by the payment timeout. The payment needs a refund or the order needs reinstating.",
                    tranRef, order.Id);
                AddOrderNote(
                    order, now,
                    "MadfoatCom payment confirmed after the order was canceled by the payment timeout — refund or reinstate manually.");
            }
            else
            {
                SetOrderStatus(order, OrderStatus.PaymentReceived, now, "MadfoatCom payment received.");
            }

            await _db.SaveChangesAsync(cancellationToken);
            return (Result.Ok(new GatewayPaymentResult(payment.Id, order.Id, true, tranRef)), true);
        }

        // Still undecided — an asynchronous method, or the shopper hasn't finished paying. Record
        // nothing, so a later IPN, the shopper coming back, or the next sweep can still settle it.
        // Writing a failure here would strand an order that is about to be paid.
        if (transaction.IsPending)
        {
            return (Result.Ok(new GatewayPaymentResult(attempt.Id, order.Id, false, tranRef)), false);
        }

        // Declined / cancelled / voided / expired: fail this attempt only. The order stays
        // PendingPayment so the shopper can retry, here or with another method.
        var failure = RecordSettlement(
            attempt,
            PaymentStatus.Failed,
            string.IsNullOrWhiteSpace(transaction.ResponseMessage)
                ? $"MadfoatCom payment was not completed (status {transaction.ResponseStatus})."
                : transaction.ResponseMessage,
            now);

        await _db.SaveChangesAsync(cancellationToken);
        return (Result.Ok(new GatewayPaymentResult(failure.Id, order.Id, false, tranRef)), true);
    }

    public async Task<int> ReconcilePendingPayTabsPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var queryBefore = now - _options.ReconciliationGrace;
        var voidBefore = now - _options.PendingPaymentTimeout;

        // Two guards, both load-bearing. The order must still be awaiting payment — that retires an
        // attempt the moment its order moves on (paid, or canceled by this very sweep), including the
        // rows this sweep just wrote. And no settlement row may already carry this tran_ref — that
        // leaves a declined attempt alone so the shopper can retry the order with a fresh transaction.
        var attempts = await _db.Payments
            .Include(p => p.Order)
            .ThenInclude(o => o.OrderHistories)
            .Where(p => p.PaymentMethod == MadfoatCom
                && p.Status == PaymentStatus.PendingExecution
                && p.CreatedOn <= queryBefore
                && p.Order.OrderStatus == OrderStatus.PendingPayment
                && !_db.Payments.Any(s =>
                    s.OrderId == p.OrderId
                    && s.Id != p.Id
                    && s.PaymentMethod == MadfoatCom
                    && s.Status != PaymentStatus.PendingExecution
                    && s.GatewayTransactionId == p.GatewayTransactionId))
            .OrderBy(p => p.Id)
            .Take(ReconciliationBatchSize)
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0)
        {
            return 0;
        }

        // Only an order's newest attempt may be voided. A shopper who abandons one hosted page and
        // starts again leaves an older attempt behind that ages past the timeout while they are still
        // paying on the newer one — voiding that stale row would cancel an order mid-payment. The
        // newest attempt is looked up rather than read off the batch, because the live one is exactly
        // the row the grace filter holds back.
        var orderIds = attempts.Select(a => a.OrderId).Distinct().ToList();
        var newestAttempts = (await _db.Payments
            .Where(p => p.PaymentMethod == MadfoatCom
                && p.Status == PaymentStatus.PendingExecution
                && orderIds.Contains(p.OrderId))
            .GroupBy(p => p.OrderId)
            .Select(g => g.Max(p => p.Id))
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var decided = 0;

        foreach (var attempt in attempts)
        {
            var timedOut = attempt.CreatedOn <= voidBefore && newestAttempts.Contains(attempt.Id);

            if (!string.IsNullOrWhiteSpace(attempt.GatewayTransactionId))
            {
                var (result, gatewayDecided) = await SettlePayTabsAsync(
                    attempt.GatewayTransactionId!, cancellationToken);

                if (gatewayDecided)
                {
                    decided++;
                    continue;
                }

                // Undecided. Void only on PayTabs' own word that it is still pending: when the query
                // itself failed we may simply be unable to reach them, and cancelling a shopper's
                // order over a network blip is far worse than leaving it for the next sweep.
                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Could not reconcile MadfoatCom payment {PaymentId} for order {OrderId}: {Error}",
                        attempt.Id, attempt.OrderId, result.Error);
                    continue;
                }
            }

            // Either PayTabs still calls it pending, or the page was never created (no tran_ref) so
            // there is nothing to ask about. Past the timeout both mean the shopper is not coming back.
            if (!timedOut)
            {
                continue;
            }

            await VoidTimedOutAttemptAsync(attempt, now, cancellationToken);
            decided++;
        }

        return decided;
    }

    /// <summary>
    /// Closes out an attempt nobody ever completed: the payment is voided and the order canceled and
    /// restocked, so an abandoned checkout can't hold stock indefinitely.
    /// </summary>
    private async Task VoidTimedOutAttemptAsync(
        Payment attempt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var order = attempt.Order;
        var minutes = _options.PendingPaymentTimeout.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);

        RecordSettlement(
            attempt,
            PaymentStatus.Voided,
            $"Voided after {minutes} minutes with no result from MadfoatCom.",
            now);

        SetOrderStatus(
            order, OrderStatus.Canceled, now, "MadfoatCom payment timed out; order canceled and stock restored.");

        // CancelOrderAsync restocks each tracked line and saves — the settlement row and the history
        // entry above ride along in that same SaveChanges.
        await _orders.CancelOrderAsync(order, cancellationToken);

        _logger.LogInformation(
            "Voided timed-out MadfoatCom payment {PaymentId} and canceled order {OrderId}.",
            attempt.Id, order.Id);
    }

    /// <summary>
    /// Records the gateway's verdict as a <b>new</b> row in <c>Payment</c> rather than overwriting the
    /// attempt. The attempt row keeps <see cref="PaymentStatus.PendingExecution"/> as the record of
    /// "the shopper was sent to the gateway"; the row added here carries the outcome, its message and
    /// the same <c>tran_ref</c>, so the payments log reads attempt → outcome for each transaction.
    /// </summary>
    private Payment RecordSettlement(Payment attempt, int status, string? failureMessage, DateTimeOffset now)
    {
        var settlement = new Payment
        {
            OrderId = attempt.OrderId,
            Amount = attempt.Amount,
            PaymentFee = attempt.PaymentFee,
            PaymentMethod = attempt.PaymentMethod,
            GatewayTransactionId = attempt.GatewayTransactionId,
            Status = status,
            FailureMessage = failureMessage,
            CreatedOn = now,
            LatestUpdatedOn = now
        };

        _db.Payments.Add(settlement);
        attempt.LatestUpdatedOn = now;
        return settlement;
    }

    /// <summary>
    /// Registers the order with PayTabs and returns their hosted payment page. Unlike the stub
    /// gateways this always talks to PayTabs for real — a demo profile is still a live API call.
    /// </summary>
    private async Task<Result<GatewayInitiationResult>> CreatePayTabsPageAsync(
        Order order,
        Payment payment,
        GatewaySettings settings,
        string returnUrl,
        string? language,
        CancellationToken cancellationToken)
    {
        if (!settings.HasPayTabsKeys)
        {
            return Result.Fail<GatewayInitiationResult>(
                "MadfoatCom is not configured — set the PayTabs profile ID and server key.");
        }

        var apiBase = _options.PublicApiBaseUrl.TrimEnd('/');
        var returnArg = Uri.EscapeDataString(string.IsNullOrWhiteSpace(returnUrl) ? "/account" : returnUrl);

        // PayTabs form-POSTs the shopper's browser to `return`, which no SPA route can accept, so it
        // lands on the API and is redirected on to the storefront from there. `callback` is the
        // server-to-server IPN: PayTabs validates it at page creation and rejects a loopback host
        // outright (code 210 "Invalid Callback URL"), so it is omitted entirely in local development —
        // where it could never be delivered anyway — and settlement rides on the return leg alone.
        var payTabsReturn = $"{apiBase}/api/payments/paytabs/return?orderId={order.Id}&returnUrl={returnArg}";
        var payTabsCallback = IsLoopback(apiBase) ? null : $"{apiBase}/api/payments/paytabs/callback";

        var party = await BuildPayTabsPartyAsync(order.Id, cancellationToken);

        try
        {
            var page = await _payTabs.CreateHostedPageAsync(
                new PayTabsPageRequest(
                    BaseUrl: settings.PayTabsBaseUrl,
                    ProfileId: settings.PayTabsProfileId,
                    ServerKey: settings.PayTabsServerKey,
                    // Unique per attempt: PayTabs rejects a cart_id it has already seen, so reusing the
                    // order id alone would block a shopper retrying after a declined card.
                    CartId: $"{order.Id}-{payment.Id}",
                    CartDescription: $"Order #{order.Id}",
                    Currency: settings.Currency.ToUpperInvariant(),
                    Amount: payment.Amount,
                    ReturnUrl: payTabsReturn,
                    CallbackUrl: payTabsCallback,
                    Language: NormalizeLanguage(language),
                    Customer: party,
                    Shipping: party),
                cancellationToken);

            // Persist the tran_ref so the return page and the IPN can locate and settle this payment.
            payment.GatewayTransactionId = page.TranRef;
            payment.LatestUpdatedOn = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            return Result.Ok(new GatewayInitiationResult(
                payment.Id, order.Id, MadfoatCom, page.RedirectUrl, IsSandbox: false));
        }
        catch (PayTabsException ex)
        {
            _logger.LogError(
                ex, "PayTabs page creation failed for order {OrderId} (code {Code}).", order.Id, ex.Code);
            return Result.Fail<GatewayInitiationResult>(
                "Could not start the MadfoatCom payment. Check the PayTabs profile ID, server key, region and currency.");
        }
    }

    /// <summary>
    /// The shopper's details for PayTabs' <c>customer_details</c>, read from the order's shipping
    /// address. Returns null when the order has no address, in which case PayTabs collects what it
    /// needs on its own page.
    /// </summary>
    private async Task<PayTabsParty?> BuildPayTabsPartyAsync(long orderId, CancellationToken cancellationToken)
    {
        var details = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.ShippingAddress.ContactName,
                o.ShippingAddress.Phone,
                o.ShippingAddress.AddressLine1,
                o.ShippingAddress.City,
                o.ShippingAddress.ZipCode,
                o.ShippingAddress.CountryId,
                // Guests have no account email; the one they gave at checkout lives on the order.
                Email = o.GuestEmail ?? o.Customer.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (details == null)
        {
            return null;
        }

        return new PayTabsParty(
            Name: details.ContactName,
            Email: details.Email,
            Phone: details.Phone,
            Street1: details.AddressLine1,
            City: details.City,
            // Deliberately omitted. PayTabs normalises `state` to a two-letter code, our governorate
            // names are Arabic (see Store.Migrator/12_localize_governorates.sql), and with
            // hide_shipping the page never shows an address — so sending it can only cause a
            // validation failure, never help.
            State: null,
            // Country.Id is already the ISO 3166-1 alpha-2 code PayTabs expects.
            Country: details.CountryId,
            Zip: details.ZipCode);
    }

    /// <summary>True when <paramref name="baseUrl"/> points at this machine and no gateway could reach it.</summary>
    private static bool IsLoopback(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback;

    /// <summary>Maps a request culture onto the two languages PayTabs' hosted page supports.</summary>
    private static string NormalizeLanguage(string? language) =>
        (language ?? string.Empty).StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";

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

    /// <summary>Appends a history entry without moving the order — used to flag it for a human.</summary>
    private static void AddOrderNote(Order order, DateTimeOffset now, string note)
    {
        order.OrderHistories.Add(new OrderHistory
        {
            OrderId = order.Id,
            OldStatus = order.OrderStatus,
            NewStatus = order.OrderStatus,
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
