using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Store.Application.Messaging;
using Store.Application.Payments;
using Store.Domain;

namespace Store.Application.Auth;

/// <summary>
/// Default <see cref="IPasswordResetService"/>. Reuses <see cref="PaymentsOptions.StorefrontBaseUrl"/> as the
/// storefront origin (the same "where does the SPA live" config already used for Stripe return URLs) to build
/// an absolute <c>/reset-password?email=...&amp;token=...</c> link.
/// </summary>
public sealed class PasswordResetService : IPasswordResetService
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailQueueService _emailQueue;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly PaymentsOptions _paymentsOptions;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        UserManager<User> userManager,
        IEmailQueueService emailQueue,
        IRefreshTokenService refreshTokenService,
        PaymentsOptions paymentsOptions,
        ILogger<PasswordResetService> logger)
    {
        _userManager = userManager;
        _emailQueue = emailQueue;
        _refreshTokenService = refreshTokenService;
        _paymentsOptions = paymentsOptions;
        _logger = logger;
    }

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || user.IsDeleted)
        {
            // No account enumeration: silently do nothing. The caller always reports success.
            return;
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = BuildResetUrl(user.Email ?? email, token);

            var tokens = new Dictionary<string, string?>
            {
                ["Customer.FullName"] = string.IsNullOrWhiteSpace(user.FullName) ? "Customer" : user.FullName,
                ["Customer.PasswordResetUrl"] = resetUrl
            };

            await _emailQueue.EnqueueAsync(
                "Customer.PasswordReset", tokens, user.Email ?? email, user.FullName,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: a broken mail queue must never surface as a failed/enumerable forgot-password call.
            _logger.LogWarning(ex, "Failed to send password-reset email for user {UserId}.", user.Id);
        }
    }

    public async Task<IdentityResult> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || user.IsDeleted)
        {
            // Same message an invalid/expired token would produce — avoids confirming account existence.
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidToken",
                Description = "Invalid or expired password reset token."
            });
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        // Revoke any outstanding refresh token so existing sessions can't outlive the password change.
        // Mirrors AuthController.Logout — IRefreshTokenService has no revoke-by-user method to call instead.
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        await _userManager.UpdateAsync(user);

        return result;
    }

    private string BuildResetUrl(string email, string token)
    {
        var baseUrl = _paymentsOptions.StorefrontBaseUrl.TrimEnd('/');
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
    }
}
