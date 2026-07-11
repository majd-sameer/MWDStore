using Microsoft.EntityFrameworkCore;
using Store.Data;

namespace Store.Application.Auditing;

/// <summary>
/// Who created and who last modified a single entity, derived from the append-only
/// <c>AuditLog</c> trail (no per-row columns exist on most entities). "Created by" is the actor of
/// the entity's first <c>Create</c> action; "modified by" is the actor of its most recent
/// <c>Create</c>/<c>Update</c> action. All fields are null when the entity predates auditing (e.g.
/// seeded rows) or has never been touched through the admin API.
/// </summary>
public sealed record AuditStamp(string? CreatedBy, DateTime? CreatedOn, string? ModifiedBy, DateTime? ModifiedOn);

/// <summary>
/// The created/modified stamps for a batch of entities of one type, keyed by id. Lookups for an
/// unknown id return nulls rather than throwing, so callers can project unconditionally.
/// </summary>
public sealed class AuditStampSet
{
    public static readonly AuditStampSet Empty = new(new Dictionary<long, AuditStamp>());

    private readonly IReadOnlyDictionary<long, AuditStamp> _byId;

    public AuditStampSet(IReadOnlyDictionary<long, AuditStamp> byId) => _byId = byId;

    public AuditStamp Get(long id) =>
        _byId.TryGetValue(id, out var stamp) ? stamp : new AuditStamp(null, null, null, null);

    public string? CreatedBy(long id) => Get(id).CreatedBy;

    public string? ModifiedBy(long id) => Get(id).ModifiedBy;
}

/// <summary>
/// Resolves "created by" / "modified by" for admin list projections from the audit trail, in one
/// batched query per list. The <paramref name="entityType"/> is the CLR entity name recorded on the
/// log (e.g. <c>nameof(Brand)</c>), matching <c>AuditLog.EntityType</c>.
/// </summary>
public interface IAuditStampReader
{
    Task<AuditStampSet> ReadAsync(
        string entityType, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AuditStampReader : IAuditStampReader
{
    private const string CreateAction = "Create";
    private const string UpdateAction = "Update";

    private readonly StoreDbContext _db;

    public AuditStampReader(StoreDbContext db) => _db = db;

    public async Task<AuditStampSet> ReadAsync(
        string entityType, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return AuditStampSet.Empty;
        }

        // De-duplicate so a repeated id doesn't bloat the IN (...) list.
        var idSet = ids as HashSet<long> ?? new HashSet<long>(ids);

        var rows = await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityType == entityType
                && l.EntityId != null
                && idSet.Contains(l.EntityId.Value)
                && (l.Action == CreateAction || l.Action == UpdateAction))
            .Select(l => new { Id = l.EntityId!.Value, l.Action, l.UserName, l.CreatedOn })
            .ToListAsync(cancellationToken);

        var byId = rows
            .GroupBy(r => r.Id)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var created = g
                        .Where(r => r.Action == CreateAction)
                        .OrderBy(r => r.CreatedOn)
                        .FirstOrDefault();
                    var modified = g
                        .OrderByDescending(r => r.CreatedOn)
                        .First();

                    return new AuditStamp(
                        created?.UserName, created?.CreatedOn, modified.UserName, modified.CreatedOn);
                });

        return new AuditStampSet(byId);
    }
}
