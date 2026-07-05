using Microsoft.Extensions.Logging;

namespace Store.Application.Scheduling;

/// <summary>
/// Trivial proof-of-life task: logs one line on a fixed interval. Exists to exercise the scheduled-task
/// framework end-to-end (registration, DI scoping, config overrides) with zero side effects — a template
/// for real tasks (e.g. draining an email queue) to copy.
/// </summary>
public sealed class HeartbeatTask : IScheduledTask
{
    private readonly ILogger<HeartbeatTask> _logger;
    private readonly TimeProvider _timeProvider;

    public HeartbeatTask(ILogger<HeartbeatTask> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public string Name => "Heartbeat";

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Heartbeat at {UtcNow:O}", _timeProvider.GetUtcNow());
        return Task.CompletedTask;
    }
}
