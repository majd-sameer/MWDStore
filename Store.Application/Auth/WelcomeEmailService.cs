using Microsoft.Extensions.Logging;
using Store.Application.Messaging;
using Store.Domain;

namespace Store.Application.Auth;

/// <summary>
/// Default <see cref="IWelcomeEmailService"/>. Enqueues the <c>Customer.Welcome</c> template through
/// <see cref="IEmailQueueService"/>. Mirrors <see cref="PasswordResetService"/>'s best-effort pattern: a
/// missing template, inactive template, or any other enqueue failure is logged and swallowed so a broken
/// mail queue never fails registration.
/// </summary>
public sealed class WelcomeEmailService : IWelcomeEmailService
{
    private readonly IEmailQueueService _emailQueue;
    private readonly ILogger<WelcomeEmailService> _logger;

    public WelcomeEmailService(IEmailQueueService emailQueue, ILogger<WelcomeEmailService> logger)
    {
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task SendWelcomeEmailAsync(User user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogInformation("Skipping welcome email for user {UserId}: no email on file.", user.Id);
            return;
        }

        try
        {
            var name = string.IsNullOrWhiteSpace(user.FullName) ? "Customer" : user.FullName;
            var tokens = new Dictionary<string, string?>
            {
                ["Customer.Name"] = name
            };

            await _emailQueue.EnqueueAsync(
                "Customer.Welcome", tokens, user.Email, user.FullName, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to enqueue welcome email for user {UserId}.", user.Id);
        }
    }
}
