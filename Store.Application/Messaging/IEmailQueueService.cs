using System.Collections.Generic;

namespace Store.Application.Messaging;

/// <summary>
/// Renders + enqueues transactional emails and drains the queue.
/// <para>
/// <see cref="ProcessQueueAsync"/> is deliberately NOT wrapped in a hosted background service — an external
/// scheduler owns the cadence and calls it on an interval. The method is idempotent and safe to call
/// repeatedly: it only picks up emails that are still pending (<c>SentOn == null</c> and
/// <c>SentTries &lt; MaxTries</c>), so concurrent-safe at the row level and re-entrant across calls.
/// </para>
/// </summary>
public interface IEmailQueueService
{
    /// <summary>
    /// Looks up the active <c>MessageTemplate</c> named <paramref name="templateName"/>, renders its subject
    /// and body with <paramref name="tokens"/>, and inserts a <c>QueuedEmail</c> for <paramref name="to"/>.
    /// Returns the id of the queued row. Throws if the template is missing or inactive.
    /// </summary>
    Task<long> EnqueueAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        string to,
        string? toName = null,
        long? emailAccountId = null,
        int priority = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drains pending queued emails (up to the configured batch size), sending each via the SMTP sender.
    /// A success stamps <c>SentOn</c>; a failure increments <c>SentTries</c> and records <c>LastError</c>,
    /// leaving the row to be retried until <c>MaxTries</c> is reached. Returns the number sent successfully.
    /// </summary>
    Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default);
}
