using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Store.Application.Scheduling;

/// <summary>
/// Discovers every scheduled task registered in DI (via the singleton
/// <see cref="ScheduledTaskRegistration"/> markers that
/// <see cref="ScheduledTaskServiceCollectionExtensions.AddScheduledTask{T}"/> adds) and runs each on
/// its own interval for the lifetime of the host. Task metadata (Name, default Interval) is read once,
/// lazily, by resolving each task from a short-lived scope — the runner is a singleton and must never
/// inject the scoped task instances directly.
///
/// Design notes:
/// <list type="bullet">
/// <item>Error isolation — an exception from one task's <see cref="IScheduledTask.ExecuteAsync"/> is
/// caught and logged; it never propagates to the runner's loop or affects other tasks.</item>
/// <item>No overlap — if a task's previous run hasn't finished when its interval next elapses, that
/// pass is skipped for that task (not queued, not run concurrently with itself).</item>
/// <item>Fresh scope per execution — each run resolves the task by type from a new
/// <see cref="IServiceScope"/>, so tasks may depend on scoped services (e.g. <c>StoreDbContext</c>)
/// safely, the same way a request does.</item>
/// <item>Testability — the scheduling decision (which tasks are due, run/skip/error handling) lives in
/// <see cref="RunDueTasksOnceAsync"/>, a single pass callable directly by tests against an injected
/// <see cref="TimeProvider"/> that can be advanced manually. <see cref="ExecuteAsync"/> is a thin loop
/// that calls it on a fixed poll tick and is not itself unit tested.</item>
/// </list>
/// </summary>
public sealed class ScheduledTaskRunner : BackgroundService
{
    /// <summary>How often the host loop re-checks which tasks are due. Independent of any task's own interval.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<ScheduledTaskRegistration> _registrations;
    private readonly IOptions<ScheduledTaskOptions> _options;
    private readonly ILogger<ScheduledTaskRunner> _logger;
    private readonly TimeProvider _timeProvider;
    private List<TaskState>? _states;

    public ScheduledTaskRunner(
        IServiceScopeFactory scopeFactory,
        IEnumerable<ScheduledTaskRegistration> registrations,
        IOptions<ScheduledTaskOptions> options,
        ILogger<ScheduledTaskRunner> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _registrations = [.. registrations];
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>Reads each registered task's Name and default Interval by resolving it once from a
    /// short-lived scope. Called lazily on the first scheduling pass (single-threaded caller).</summary>
    private List<TaskState> BuildStates()
    {
        using var scope = _scopeFactory.CreateScope();
        return
        [
            .. _registrations.Select(r =>
            {
                var task = (IScheduledTask)scope.ServiceProvider.GetRequiredService(r.TaskType);
                return new TaskState(task.Name, r.TaskType, task.Interval);
            })
        ];
    }

    private ScheduledTaskOptions CurrentOptions => _options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, _timeProvider);
        do
        {
            await RunDueTasksOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Runs exactly one scheduling pass: for every registered task that is enabled and whose interval
    /// has elapsed since its last run, executes it (from a fresh DI scope) unless a previous run of that
    /// same task is still in progress. Never throws — task failures are caught and logged.
    /// Public so tests can pump scheduling passes deterministically instead of sleeping in real time;
    /// <see cref="ExecuteAsync"/> simply calls this on a fixed poll tick for the lifetime of the host.
    /// </summary>
    public Task RunDueTasksOnceAsync(CancellationToken cancellationToken)
    {
        var options = CurrentOptions;
        if (!options.Enabled)
        {
            return Task.CompletedTask;
        }

        var now = _timeProvider.GetUtcNow();
        List<Task>? started = null;
        foreach (var state in _states ??= BuildStates())
        {
            var entry = options.Tasks.GetValueOrDefault(state.Name);
            if (entry is { Enabled: false })
            {
                continue;
            }

            var interval = entry?.Interval ?? state.DefaultInterval;
            if (state.NextRunAt is { } nextRunAt && now < nextRunAt)
            {
                continue;
            }

            if (!state.TryBeginRun())
            {
                // Previous run still in progress — skip this pass rather than overlap or queue.
                continue;
            }

            state.NextRunAt = now + interval;
            (started ??= []).Add(RunOneAsync(state, cancellationToken));
        }

        // Each task's own run is fire-and-forget with respect to *other* tasks (one slow task must not
        // delay the rest), but this pass as a whole awaits everything it started this tick so that
        // callers driving the scheduler manually (tests, and effectively the host loop) observe a
        // completed run before moving on — except a run left in-flight by a still-running previous
        // pass, which callers may legitimately choose not to await.
        return started is null ? Task.CompletedTask : Task.WhenAll(started);
    }

    private async Task RunOneAsync(TaskState state, CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetTimestamp();
        _logger.LogInformation("Scheduled task {TaskName} starting", state.Name);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var task = (IScheduledTask)scope.ServiceProvider.GetRequiredService(state.TaskType);
            await task.ExecuteAsync(cancellationToken);
            _logger.LogInformation(
                "Scheduled task {TaskName} finished in {ElapsedMs}ms",
                state.Name,
                _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Scheduled task {TaskName} failed after {ElapsedMs}ms",
                state.Name,
                _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        finally
        {
            state.EndRun();
        }
    }

    private sealed class TaskState(string name, Type taskType, TimeSpan defaultInterval)
    {
        private int _running;

        public string Name { get; } = name;

        public Type TaskType { get; } = taskType;

        public TimeSpan DefaultInterval { get; } = defaultInterval;

        public DateTimeOffset? NextRunAt { get; set; }

        /// <summary>Atomically marks the task as running, returning <c>false</c> if it was already running.</summary>
        public bool TryBeginRun() => Interlocked.CompareExchange(ref _running, 1, 0) == 0;

        public void EndRun() => Interlocked.Exchange(ref _running, 0);
    }
}
