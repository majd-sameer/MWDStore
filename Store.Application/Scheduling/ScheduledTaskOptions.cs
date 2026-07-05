namespace Store.Application.Scheduling;

/// <summary>
/// Host-configured settings for the scheduled-task framework. Bound from the <c>ScheduledTasks</c>
/// configuration section in <c>Store.Api</c>; defaults leave the framework enabled with every task
/// running on its own code-defined <see cref="IScheduledTask.Interval"/>.
/// </summary>
public sealed class ScheduledTaskOptions
{
    public const string SectionName = "ScheduledTasks";

    /// <summary>Global kill switch. When <c>false</c>, no scheduled task runs regardless of per-task settings.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Per-task overrides keyed by <see cref="IScheduledTask.Name"/>.</summary>
    public Dictionary<string, ScheduledTaskEntryOptions> Tasks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Per-task override of enablement and/or interval, keyed by task name in <see cref="ScheduledTaskOptions.Tasks"/>.</summary>
public sealed class ScheduledTaskEntryOptions
{
    /// <summary>When <c>false</c>, this task never runs. Defaults to <c>true</c> (i.e. absence from config means enabled).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Overrides the task's code-defined <see cref="IScheduledTask.Interval"/> when set.</summary>
    public TimeSpan? Interval { get; set; }
}
