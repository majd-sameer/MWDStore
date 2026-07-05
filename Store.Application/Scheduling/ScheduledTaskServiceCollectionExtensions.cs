using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Store.Application.Scheduling;

public static class ScheduledTaskServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="T"/> as a scheduled background task: the concrete type is added to
    /// DI (scoped, so it may depend on scoped services like <c>StoreDbContext</c>) and a singleton
    /// <see cref="ScheduledTaskRegistration"/> is added so <see cref="ScheduledTaskRunner"/> discovers
    /// and runs it — the runner must not inject the scoped instances directly. Also ensures the single
    /// shared runner is registered as a hosted service — safe to call once per task type; the runner
    /// itself is only ever added once regardless of how many tasks call this.
    /// </summary>
    public static IServiceCollection AddScheduledTask<T>(this IServiceCollection services)
        where T : class, IScheduledTask
    {
        services.AddScoped<T>();
        services.AddSingleton(new ScheduledTaskRegistration(typeof(T)));

        // IHostedService is a multi-registration collection by design, so a plain AddHostedService() call
        // here would register one runner instance per task type. TryAddEnumerable dedupes by
        // (ServiceType, ImplementationType), so — combined with the runner itself being a singleton —
        // only the first AddScheduledTask<T> call actually adds it; later calls are no-ops for the runner.
        services.TryAddSingleton<ScheduledTaskRunner>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ScheduledTaskRunner>(
            sp => sp.GetRequiredService<ScheduledTaskRunner>()));

        return services;
    }
}
