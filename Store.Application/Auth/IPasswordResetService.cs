using Microsoft.AspNetCore.Identity;

namespace Store.Application.Auth;

/// <summary>
/// Orchestrates the forgot-password / reset-password flow on top of ASP.NET Core Identity's built-in
/// data-protection reset tokens (<see cref="UserManager{TUser}.GeneratePasswordResetTokenAsync"/> /
/// <see cref="UserManager{TUser}.ResetPasswordAsync"/>).
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Best-effort: if an account exists for <paramref name="email"/>, generates a reset token, builds the
    /// storefront reset link, and enqueues the <c>Customer.PasswordReset</c> email. Never throws and never
    /// reveals whether the account exists — callers should always report success to the caller/UI regardless
    /// of the outcome (no account enumeration).
    /// </summary>
    Task RequestResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates <paramref name="token"/> for the account identified by <paramref name="email"/> and, if
    /// valid, sets <paramref name="newPassword"/>. On success also revokes the user's refresh token so
    /// existing sessions cannot outlive the password change.
    /// </summary>
    Task<IdentityResult> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken = default);
}
