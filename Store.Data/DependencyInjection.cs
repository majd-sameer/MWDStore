using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Store.Data.Auditing;

namespace Store.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddStoreData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<StoreDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Per-request buffer the DbContext writes captured changes into and the audit filter drains.
        services.AddScoped<IAuditContext, AuditContext>();

        return services;
    }
}
