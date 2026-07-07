using System;
using System.Collections.Generic;

namespace Store.Data.Auditing;

/// <summary>
/// One entity's captured change: its type, key, a human name, and the changed scalar properties
/// (secrets already stripped). Populated by <see cref="StoreDbContext"/> during
/// <c>SaveChangesAsync</c>, before the values are flattened by the save.
/// </summary>
public sealed class AuditChange
{
    public required string EntityType { get; init; }

    public long? EntityId { get; set; }

    public string? EntityName { get; set; }

    /// <summary>"Added" | "Modified" | "Deleted".</summary>
    public required string State { get; init; }

    public Dictionary<string, object?> OldValues { get; } = new();

    public Dictionary<string, object?> NewValues { get; } = new();

    /// <summary>How many fields this change touches — used to pick the request's primary entity.</summary>
    public int ChangedCount => Math.Max(OldValues.Count, NewValues.Count);
}

/// <summary>
/// Scoped, per-request buffer of entity changes captured by the DbContext. The audit action filter
/// reads it after a successful admin mutation and turns it into an <c>AuditLog</c> row.
/// </summary>
public interface IAuditContext
{
    IReadOnlyList<AuditChange> Changes { get; }

    void Add(AuditChange change);

    void Clear();
}

/// <inheritdoc />
public sealed class AuditContext : IAuditContext
{
    private readonly List<AuditChange> _changes = [];

    public IReadOnlyList<AuditChange> Changes => _changes;

    public void Add(AuditChange change) => _changes.Add(change);

    public void Clear() => _changes.Clear();
}
