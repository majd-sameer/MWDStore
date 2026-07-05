namespace Store.Application.Scheduling;

/// <summary>
/// A recurring background job discovered from DI and driven by <see cref="ScheduledTaskRunner"/>.
/// Implementations should be registered with <see cref="ScheduledTaskServiceCollectionExtensions.AddScheduledTask{T}"/>
/// and may be scoped or transient — the runner resolves each execution from a fresh DI scope so
/// implementations can safely depend on scoped services such as the <c>StoreDbContext</c>.
/// </summary>
public interface IScheduledTask
{
    /// <summary>
    /// Stable identifier for this task, used to match it against the <c>ScheduledTasks</c> configuration
    /// section (per-task <c>Enabled</c>/<c>Interval</c> overrides) and in log messages. Should be unique
    /// across all registered tasks.
    /// </summary>
    string Name { get; }

    /// <summary>Default time between the end of one run and the start of the next, unless overridden by configuration.</summary>
    TimeSpan Interval { get; }

    /// <summary>Runs one execution of the task. Exceptions are caught and logged by the runner — they never propagate.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
