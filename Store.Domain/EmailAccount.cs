using System;
using System.Collections.Generic;

namespace Store.Domain;

/// <summary>
/// An SMTP account transactional email is sent through. Modeled on nopCommerce's
/// <c>EmailAccount</c>: one account is flagged <see cref="IsDefault"/> and used when a
/// <see cref="QueuedEmail"/> does not name a specific account.
/// </summary>
public class EmailAccount
{
    public long Id { get; set; }

    /// <summary>SMTP server host name (e.g. <c>smtp.example.com</c>).</summary>
    public string Host { get; set; } = null!;

    /// <summary>SMTP server port (e.g. 587 for STARTTLS, 465 for implicit SSL, 25 for plain).</summary>
    public int Port { get; set; }

    /// <summary>Whether the transport should use SSL/TLS.</summary>
    public bool EnableSsl { get; set; }

    /// <summary>SMTP username; may be empty for anonymous relays.</summary>
    public string? Username { get; set; }

    /// <summary>
    /// SMTP password. Stored as-is here (encryption/secret-management is a host concern and out of
    /// scope for this foundation); keep real secrets in configuration/user-secrets, not in the row.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>The <c>From</c> mailbox address outgoing mail is sent as.</summary>
    public string Email { get; set; } = null!;

    /// <summary>The display name shown alongside <see cref="Email"/>.</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>The default account used when a queued email does not reference a specific one.</summary>
    public bool IsDefault { get; set; }

    public ICollection<QueuedEmail> QueuedEmails { get; set; } = [];
}
