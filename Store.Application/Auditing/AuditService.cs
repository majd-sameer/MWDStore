using Store.Data;
using Store.Domain;

namespace Store.Application.Auditing;

/// <inheritdoc />
public sealed class AuditService : IAuditService
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AuditService(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var row = new AuditLog
        {
            UserId = entry.UserId,
            UserName = entry.UserName,
            Role = entry.Role,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            EntityName = entry.EntityName,
            OldValuesJson = entry.OldValuesJson,
            NewValuesJson = entry.NewValuesJson,
            Area = entry.Area,
            IpAddress = entry.IpAddress,
            CorrelationId = entry.CorrelationId,
            CreatedOn = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _db.AuditLogs.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
