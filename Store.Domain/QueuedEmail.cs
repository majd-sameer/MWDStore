using System;

namespace Store.Domain;

/// <summary>
/// A single outbound email waiting to be delivered. The queue is drained by
/// <c>IEmailQueueService.ProcessQueueAsync</c>: each attempt increments <see cref="SentTries"/>, a
/// success stamps <see cref="SentOn"/>, and a failure records <see cref="LastError"/>. Once
/// <see cref="SentTries"/> reaches <see cref="MaxTries"/> the email is no longer retried. Modeled on
/// nopCommerce's <c>QueuedEmail</c>.
/// </summary>
public class QueuedEmail
{
    public long Id { get; set; }

    /// <summary>Recipient mailbox address.</summary>
    public string To { get; set; } = null!;

    /// <summary>Optional recipient display name.</summary>
    public string? ToName { get; set; }

    /// <summary>Optional BCC recipients (comma/semicolon separated), copied from the template.</summary>
    public string? Bcc { get; set; }

    /// <summary>Rendered subject line (tokens already substituted).</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Rendered body (tokens already substituted).</summary>
    public string Body { get; set; } = null!;

    /// <summary>When the email was enqueued.</summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>When the email was successfully sent; <c>null</c> while still pending.</summary>
    public DateTimeOffset? SentOn { get; set; }

    /// <summary>Number of delivery attempts made so far.</summary>
    public int SentTries { get; set; }

    /// <summary>Maximum number of delivery attempts before the email is abandoned.</summary>
    public int MaxTries { get; set; } = 3;

    /// <summary>The error message from the most recent failed attempt, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Lower numbers are sent first (nopCommerce convention: High=5, Low=0).</summary>
    public int Priority { get; set; }

    /// <summary>The account used to send; when <c>null</c> the default account is used.</summary>
    public long? EmailAccountId { get; set; }

    public EmailAccount? EmailAccount { get; set; }
}
