using Store.Domain;

namespace Store.Application.Auth;

/// <summary>
/// Sends the one-time welcome email right after a new account is created (see <c>Customer.Welcome</c>
/// message template). Best-effort by design: failures are logged and swallowed internally, so callers
/// (e.g. registration) can invoke this unconditionally without wrapping it in their own try/catch.
/// </summary>
public interface IWelcomeEmailService
{
    Task SendWelcomeEmailAsync(User user, CancellationToken cancellationToken = default);
}
