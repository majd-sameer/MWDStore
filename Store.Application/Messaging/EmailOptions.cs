namespace Store.Application.Messaging;

/// <summary>
/// Host-configured SMTP fallback + queue behaviour. Bound from the <c>Email</c> configuration section
/// in <c>Store.Api</c>. These values are only used when no default <c>EmailAccount</c> row exists in the
/// database; a seeded/default <c>EmailAccount</c> always takes precedence.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Fallback SMTP host used when the database has no default <c>EmailAccount</c>.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Fallback SMTP port.</summary>
    public int Port { get; set; } = 25;

    /// <summary>Whether the fallback transport uses SSL/TLS.</summary>
    public bool EnableSsl { get; set; }

    /// <summary>Fallback SMTP username (empty for anonymous relays).</summary>
    public string? Username { get; set; }

    /// <summary>Fallback SMTP password.</summary>
    public string? Password { get; set; }

    /// <summary>Fallback <c>From</c> address.</summary>
    public string FromEmail { get; set; } = "no-reply@mystore.local";

    /// <summary>Fallback <c>From</c> display name.</summary>
    public string FromName { get; set; } = "MyStore";

    /// <summary>Default <c>MaxTries</c> stamped on newly enqueued emails.</summary>
    public int MaxTries { get; set; } = 3;

    /// <summary>Maximum number of queued emails drained per <c>ProcessQueueAsync</c> call.</summary>
    public int BatchSize { get; set; } = 50;
}
