namespace Store.Application.Auth;

/// <summary>A freshly-issued MFA login challenge and its absolute expiry.</summary>
public sealed record MfaChallenge(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues and validates the short-lived signed artifact handed back by the login endpoint when a user has
/// TOTP two-factor enabled. The artifact is redeemed at <c>/api/auth/mfa/verify</c> together with a valid
/// authenticator (or recovery) code to obtain the normal access/refresh tokens.
/// </summary>
public interface IMfaChallengeService
{
    /// <summary>Mints a challenge bound to the given user id, valid for <see cref="MfaChallengeService.Lifetime"/>.</summary>
    MfaChallenge Create(long userId);

    /// <summary>Validates a presented challenge; returns the bound user id, or <c>null</c> if invalid/expired.</summary>
    long? Validate(string token);
}
