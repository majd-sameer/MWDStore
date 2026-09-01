using Store.Application.Payments;

namespace Store.Api.Infrastructure;

/// <summary>
/// Runs the MadfoatCom (PayTabs) reconciliation sweep on a timer, so a payment's outcome never
/// depends on the shopper's browser making it back to us.
/// </summary>
/// <remarks>
/// <para>
/// Two things go wrong without it. A shopper who pays and immediately closes the tab never triggers
/// the return leg, and the server-to-server IPN is not always a backstop — it is deliberately not
/// requested against a localhost API, and on a real host it can still be blocked or dropped. Their
/// payment would sit at <see cref="PaymentStatus.PendingExecution"/> forever with the money taken.
/// Conversely a shopper who abandons the hosted page leaves an order holding stock indefinitely.
/// The sweep asks PayTabs' query API about both, and voids what is still undecided at the timeout.
/// </para>
/// <para>
/// A scope per tick: the sweep needs the scoped <see cref="IGatewayPaymentService"/> and its
/// <c>DbContext</c>, which must not be captured by this singleton. One instance is assumed — the
/// deployment runs a single API process (see <c>DEPLOYMENT-RUNBOOK.md</c>); scaling out would want a
/// lease so two instances don't sweep the same rows at once.
/// </para>
/// </remarks>
public sealed class PaymentReconciliationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PaymentsOptions _options;
    private readonly ILogger<PaymentReconciliationService> _logger;

    public PaymentReconciliationService(
        IServiceScopeFactory scopes,
        PaymentsOptions options,
        ILogger<PaymentReconciliationService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReconciliationEnabled)
        {
            _logger.LogInformation(
                "Payment reconciliation is disabled; MadfoatCom payments will only settle via the return leg and the IPN.");
            return;
        }

        _logger.LogInformation(
            "Payment reconciliation started: every {Interval}, voiding attempts unresolved after {Timeout}.",
            _options.ReconciliationInterval, _options.PendingPaymentTimeout);

        using var timer = new PeriodicTimer(_options.ReconciliationInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var gateway = scope.ServiceProvider.GetRequiredService<IGatewayPaymentService>();

                var decided = await gateway.ReconcilePendingPayTabsPaymentsAsync(stoppingToken);
                if (decided > 0)
                {
                    _logger.LogInformation("Payment reconciliation decided {Count} pending payment(s).", decided);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad pass kill the timer — the next tick retries from a clean scope.
                _logger.LogError(ex, "Payment reconciliation pass failed.");
            }
        }
    }
}
