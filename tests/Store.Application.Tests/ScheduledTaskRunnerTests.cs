using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Store.Application.Scheduling;

namespace Store.Application.Tests;

/// <summary>
/// Verifies <see cref="ScheduledTaskRunner"/>'s scheduling semantics: error isolation between tasks,
/// no-overlap for a single task's own runs, disabled/enabled gating, and per-task interval overrides
/// from <see cref="ScheduledTaskOptions"/>. The runner's infinite <c>ExecuteAsync</c> loop is not
/// exercised directly — tests instead pump <see cref="ScheduledTaskRunner.RunDueTasksOnceAsync"/>,
/// MyStore's supported way to drive one scheduling pass deterministically, and advance a fake
/// <see cref="TimeProvider"/> instead of sleeping.
/// </summary>
public class ScheduledTaskRunnerTests
{
    /// <summary>A <see cref="TimeProvider"/> whose "now" can be advanced on demand, so interval-based
    /// due-checks are deterministic without real waiting.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTimeOffset start) => _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class RecordingTask : IScheduledTask
    {
        private readonly TaskCompletionSource? _releaseGate;

        public RecordingTask(string name, TimeSpan interval, TaskCompletionSource? releaseGate = null)
        {
            Name = name;
            Interval = interval;
            _releaseGate = releaseGate;
        }

        public string Name { get; }

        public TimeSpan Interval { get; }

        public int RunCount { get; private set; }

        public int ConcurrentRuns { get; private set; }

        public int MaxObservedConcurrency { get; private set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            RunCount++;
            ConcurrentRuns++;
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, ConcurrentRuns);
            try
            {
                if (_releaseGate is not null)
                {
                    await _releaseGate.Task;
                }
            }
            finally
            {
                ConcurrentRuns--;
            }
        }
    }

    private sealed class ThrowingTask : IScheduledTask
    {
        public string Name => "throwing-task";

        public TimeSpan Interval => TimeSpan.FromMinutes(1);

        public int Attempts { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Attempts++;
            throw new InvalidOperationException("boom");
        }
    }

    private static ScheduledTaskRunner NewRunner(
        IEnumerable<IScheduledTask> tasks,
        TimeProvider timeProvider,
        ScheduledTaskOptions? options = null)
    {
        var services = new ServiceCollection();
        foreach (var task in tasks)
        {
            services.AddSingleton(task.GetType(), task);
        }
        var provider = services.BuildServiceProvider();

        return new ScheduledTaskRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            [.. tasks.Select(t => new ScheduledTaskRegistration(t.GetType()))],
            Options.Create(options ?? new ScheduledTaskOptions()),
            NullLogger<ScheduledTaskRunner>.Instance,
            timeProvider);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_RunsRegisteredTask_OnFirstPass()
    {
        var task = new RecordingTask("sample", TimeSpan.FromMinutes(5));
        var runner = NewRunner([task], new ManualTimeProvider(DateTimeOffset.UtcNow));

        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(1, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_DoesNotRerun_BeforeIntervalElapses()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var task = new RecordingTask("sample", TimeSpan.FromMinutes(5));
        var runner = NewRunner([task], clock);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1)); // still < 5 minute interval
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(1, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_RunsAgain_AfterIntervalElapses()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var task = new RecordingTask("sample", TimeSpan.FromMinutes(5));
        var runner = NewRunner([task], clock);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(5));
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(2, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_ErrorInOneTask_DoesNotStopAnotherTask()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var throwing = new ThrowingTask();
        var healthy = new RecordingTask("healthy", TimeSpan.FromMinutes(5));
        var runner = NewRunner([throwing, healthy], clock);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(1, throwing.Attempts);
        Assert.Equal(1, healthy.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_ErrorInTask_DoesNotThrow_AndTaskRunsAgainNextInterval()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var throwing = new ThrowingTask();
        var runner = NewRunner([throwing], clock);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(2, throwing.Attempts);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_SkipsRun_WhenPreviousRunStillInProgress()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var gate = new TaskCompletionSource();
        var task = new RecordingTask("slow", TimeSpan.FromMinutes(5), gate);
        var runner = NewRunner([task], clock);

        // First pass starts the long-running execution but does not await its completion.
        var firstPass = runner.RunDueTasksOnceAsync(CancellationToken.None);

        // Interval elapses while the first run is still in flight; a second pass must skip, not overlap.
        clock.Advance(TimeSpan.FromMinutes(10));
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        gate.SetResult();
        await firstPass;

        Assert.Equal(1, task.RunCount);
        Assert.Equal(1, task.MaxObservedConcurrency);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_RunsAgain_AfterOverlapSkippedRunCompletes()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var gate = new TaskCompletionSource();
        var task = new RecordingTask("slow", TimeSpan.FromMinutes(5), gate);
        var runner = NewRunner([task], clock);

        var firstPass = runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(10));
        await runner.RunDueTasksOnceAsync(CancellationToken.None); // skipped: still running

        gate.SetResult();
        await firstPass;

        clock.Advance(TimeSpan.FromMinutes(5));
        await runner.RunDueTasksOnceAsync(CancellationToken.None); // now due again and not running

        Assert.Equal(2, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_NeverRuns_WhenTaskDisabledInConfig()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var task = new RecordingTask("sample", TimeSpan.FromMinutes(5));
        var options = new ScheduledTaskOptions
        {
            Tasks = { ["sample"] = new ScheduledTaskEntryOptions { Enabled = false } }
        };
        var runner = NewRunner([task], clock, options);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(10));
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(0, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_NeverRunsAnyTask_WhenGloballyDisabled()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var task = new RecordingTask("sample", TimeSpan.FromMinutes(5));
        var options = new ScheduledTaskOptions { Enabled = false };
        var runner = NewRunner([task], clock, options);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(0, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_UsesConfiguredIntervalOverride_InsteadOfCodeDefault()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var task = new RecordingTask("sample", TimeSpan.FromMinutes(5)); // code default: 5 min
        var options = new ScheduledTaskOptions
        {
            Tasks = { ["sample"] = new ScheduledTaskEntryOptions { Interval = TimeSpan.FromMinutes(1) } }
        };
        var runner = NewRunner([task], clock, options);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1)); // would NOT be due yet under the 5-min code default
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(2, task.RunCount);
    }

    [Fact]
    public async Task RunDueTasksOnceAsync_ResolvesTaskFromFreshScope_ForEachExecution()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        // Mirrors AddScheduledTask<T>(): the concrete type is registered scoped (so the runner can
        // re-resolve it by type from a fresh scope on every run) and discovery happens through the
        // singleton ScheduledTaskRegistration marker, never the scoped instance itself.
        services.AddScoped<ScopeCapturingTask>();
        var provider = services.BuildServiceProvider();

        var runner = new ScheduledTaskRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            [new ScheduledTaskRegistration(typeof(ScopeCapturingTask))],
            Options.Create(new ScheduledTaskOptions()),
            NullLogger<ScheduledTaskRunner>.Instance,
            clock);

        await runner.RunDueTasksOnceAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await runner.RunDueTasksOnceAsync(CancellationToken.None);

        Assert.Equal(2, ScopeCapturingTask.ObservedProbeIds.Count);
        Assert.NotEqual(ScopeCapturingTask.ObservedProbeIds[0], ScopeCapturingTask.ObservedProbeIds[1]);
    }

    private sealed class ScopedProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class ScopeCapturingTask : IScheduledTask
    {
        public static List<Guid> ObservedProbeIds { get; } = [];

        // Depends on a scoped service; the runner must resolve a fresh instance (and thus a fresh
        // DI scope) for every execution rather than reusing one scope for the process lifetime.
        private readonly ScopedProbe _probe;

        public ScopeCapturingTask(ScopedProbe probe) => _probe = probe;

        public string Name => "scope-capturing";

        public TimeSpan Interval => TimeSpan.FromMinutes(1);

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ObservedProbeIds.Add(_probe.Id);
            return Task.CompletedTask;
        }
    }
}
