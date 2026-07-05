namespace Store.Application.Scheduling;

/// <summary>
/// Singleton DI marker added by
/// <see cref="ScheduledTaskServiceCollectionExtensions.AddScheduledTask{T}"/>. The singleton
/// <see cref="ScheduledTaskRunner"/> discovers task types through these instead of injecting the
/// (scoped) <see cref="IScheduledTask"/> instances themselves, which DI scope validation forbids.
/// </summary>
public sealed record ScheduledTaskRegistration(Type TaskType);
