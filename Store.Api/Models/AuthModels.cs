using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

public sealed class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }
}

public sealed class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public required string AccessToken { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }

    public required long UserId { get; set; }

    public required string Email { get; set; }

    public string? FullName { get; set; }
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Returned by <c>/api/account/mfa/setup</c>: the shared secret (both raw and formatted) to enrol an
/// authenticator app, plus the <c>otpauth://</c> URI most apps consume from a QR code.</summary>
public sealed class MfaSetupResponse
{
    /// <summary>The authenticator secret grouped in blocks of four for manual entry (lower-cased).</summary>
    public required string SharedKey { get; set; }

    /// <summary>The <c>otpauth://totp/...</c> provisioning URI (issuer "MyStore", account = user email).</summary>
    public required string AuthenticatorUri { get; set; }
}

public sealed class MfaEnableRequest
{
    /// <summary>The current 6-digit code from the authenticator app that just enrolled the shared key.</summary>
    [Required]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Returned once, on successful enable: the one-time recovery codes. Never retrievable again.</summary>
public sealed class MfaEnableResponse
{
    public required IReadOnlyList<string> RecoveryCodes { get; set; }
}

public sealed class MfaDisableRequest
{
    /// <summary>A current authenticator code, or an unused recovery code, proving control of the second factor.</summary>
    [Required]
    public string Code { get; set; } = string.Empty;
}

public sealed class MfaVerifyRequest
{
    /// <summary>The short-lived challenge token returned by <c>/api/auth/login</c> when MFA is required.</summary>
    [Required]
    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>A current authenticator code, or an unused recovery code.</summary>
    [Required]
    public string Code { get; set; } = string.Empty;
}

/// <summary>The 200 body <c>/api/auth/login</c> returns instead of tokens when the account has MFA enabled.</summary>
public sealed class MfaChallengeResponse
{
    public bool MfaRequired { get; init; } = true;

    public required string ChallengeToken { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AccountProfile
{
    public long Id { get; set; }

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];
}

public sealed class UpdateProfileRequest
{
    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }
}
