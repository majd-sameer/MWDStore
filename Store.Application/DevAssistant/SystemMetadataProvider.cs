using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Data;

namespace Store.Application.DevAssistant;

/// <summary>The single source of structural truth for the assistant (spec §2.3).</summary>
public interface ISystemMetadataProvider
{
    SystemMetadataSnapshot Snapshot { get; }
}

/// <summary>
/// Registered as a singleton; builds one immutable snapshot on first use from the EF model
/// (via a short-lived scope) plus the reflected API surface, then serves it for the process
/// lifetime. Snapshot immutability is the truthfulness guarantee (SEC-13); construction is
/// defensive so a partial failure degrades an answer domain instead of the store (SEC-15).
/// </summary>
public sealed class SystemMetadataProvider : ISystemMetadataProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IApiSurfaceSource _apiSurface;
    private readonly TimeProvider _time;
    private readonly Lazy<SystemMetadataSnapshot> _snapshot;

    public SystemMetadataProvider(IServiceScopeFactory scopeFactory, IApiSurfaceSource apiSurface, TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _apiSurface = apiSurface;
        _time = time;
        _snapshot = new Lazy<SystemMetadataSnapshot>(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public SystemMetadataSnapshot Snapshot => _snapshot.Value;

    private SystemMetadataSnapshot Build()
    {
        var notices = new List<string>();
        var endpoints = _apiSurface.Scan(notices);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();

        // SEC-17: the feature's single sanctioned database touch — compare the migrations assembly
        // against __EFMigrationsHistory once, at snapshot build.
        bool? pendingMigrations = null;
        try
        {
            pendingMigrations = db.Database.GetPendingMigrations().Any();
        }
        catch (Exception ex)
        {
            notices.Add($"Pending-migration check unavailable: {ex.Message}");
        }

        return SnapshotBuilder.Build(
            db.Model,
            endpoints,
            _apiSurface.AssemblyVersion,
            _time.GetUtcNow(),
            pendingMigrations,
            notices,
            typeof(StoreDbContext).Assembly);
    }
}
