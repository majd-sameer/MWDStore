using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Data;
using Store.Domain;

namespace Store.Application.Messaging;

/// <summary>
/// Default <see cref="IEmailQueueService"/>. Enqueue renders a <c>MessageTemplate</c> into a persisted
/// <c>QueuedEmail</c>; ProcessQueue drains pending rows through the <see cref="IEmailSender"/>, applying
/// the <c>SentTries</c>/<c>MaxTries</c> retry policy and recording <c>LastError</c> on failure.
/// </summary>
public sealed class EmailQueueService : IEmailQueueService
{
    private readonly StoreDbContext _db;
    private readonly ITemplateRenderer _renderer;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailQueueService> _logger;

    public EmailQueueService(
        StoreDbContext db,
        ITemplateRenderer renderer,
        IEmailSender sender,
        EmailOptions options,
        TimeProvider timeProvider,
        ILogger<EmailQueueService> logger)
    {
        _db = db;
        _renderer = renderer;
        _sender = sender;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<long> EnqueueAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        string to,
        string? toName = null,
        long? emailAccountId = null,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.MessageTemplates
            .FirstOrDefaultAsync(t => t.Name == templateName, cancellationToken)
            ?? throw new InvalidOperationException($"Message template '{templateName}' was not found.");

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Message template '{templateName}' is not active.");
        }

        // Base tokens every template may reference without each caller having to supply them.
        // Caller-provided tokens win on collision.
        var allTokens = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Store.Name"] = _options.FromName
        };
        foreach (var (key, value) in tokens)
        {
            allTokens[key] = value;
        }

        var queued = new QueuedEmail
        {
            To = to,
            ToName = toName,
            Bcc = template.BccEmailAddresses,
            Subject = _renderer.Render(template.Subject, allTokens),
            Body = _renderer.Render(template.Body, allTokens),
            CreatedOn = _timeProvider.GetUtcNow(),
            SentTries = 0,
            MaxTries = _options.MaxTries,
            Priority = priority,
            EmailAccountId = emailAccountId
        };

        _db.QueuedEmails.Add(queued);
        await _db.SaveChangesAsync(cancellationToken);
        return queued.Id;
    }

    public async Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        // "Still pending": not yet sent and hasn't exhausted its retries. Highest priority (largest value)
        // and oldest first, capped at the configured batch size so a call does bounded work.
        var pending = await _db.QueuedEmails
            .Where(e => e.SentOn == null && e.SentTries < e.MaxTries)
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.CreatedOn)
            .ThenBy(e => e.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var email in pending)
        {
            email.SentTries++;
            try
            {
                await _sender.SendAsync(email, cancellationToken);
                email.SentOn = _timeProvider.GetUtcNow();
                email.LastError = null;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                email.LastError = ex.Message;
                _logger.LogWarning(ex,
                    "Failed to send queued email {QueuedEmailId} (attempt {Attempt}/{MaxTries}).",
                    email.Id, email.SentTries, email.MaxTries);
            }

            // Persist per-email so a mid-batch cancellation or crash doesn't lose progress or resend.
            await _db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }
}
