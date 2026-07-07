using System;
using System.Collections.Generic;

namespace Store.Data.Auditing;

/// <summary>
/// Hard-coded deny-list keeping credential material out of the audit trail. A property is redacted
/// when its name matches one of the well-known secret columns or contains a sensitive token, so a
/// value like <c>PasswordHash</c> or <c>RefreshTokenHash</c> can never be serialized into
/// <c>OldValuesJson</c>/<c>NewValuesJson</c>.
/// </summary>
public static class AuditSecrets
{
    private static readonly HashSet<string> Denied = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "RefreshTokenHash",
        "RefreshToken",
        "PasswordSalt",
        "TwoFactorSecret",
    };

    private static readonly string[] Fragments = ["password", "secret", "token", "apikey"];

    public static bool IsSecret(string propertyName)
    {
        if (Denied.Contains(propertyName))
        {
            return true;
        }

        foreach (var fragment in Fragments)
        {
            if (propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
