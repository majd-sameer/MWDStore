using Microsoft.Extensions.Logging;
using Store.Application.Scheduling;

namespace Store.Application.Messaging;

/// <summary>
/// Drains the transactional-email queue on a fixed interval by calling
/// <see cref="IEmailQueueService.ProcessQueueAsync"/>. This is the glue between the email
/// infrastructure (which only enqueues) and the scheduled-task framework (which owns timing).
/// The runner resolves this task from a fresh DI scope per run, so the scoped queue service
/// (and its DbContext) are safe here.
/// </summary>
public sealed class EmailQueueDrainTask : IScheduledTask
{
    private readonly IEmailQueueService _emailQueue;
    private readonly ILogger<EmailQueueDrainTask> _logger;

    public EmailQueueDrainTask(IEmailQueueService emailQueue, ILogger<EmailQueueDrainTask> logger)
    {
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public string Name => "EmailQueueDrain";

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var sent = await _emailQueue.ProcessQueueAsync(cancellationToken);
        if (sent > 0)
        {
            _logger.LogInformation("Email queue drain sent {Count} email(s).", sent);
        }
    }
}
