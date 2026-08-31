using System;

namespace Store.Domain;

/// <summary>
/// One active refresh token, i.e. one signed-in browser or device. A user holds many of these at
/// once, so signing in somewhere new never invalidates the sessions elsewhere — the token presented
/// on refresh identifies which session is rotating. Only the SHA-256 hash is stored; the raw token
/// lives solely in the client's httpOnly cookie.
/// </summary>
public class UserRefreshToken
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public User User { get; set; } = null!;
}
