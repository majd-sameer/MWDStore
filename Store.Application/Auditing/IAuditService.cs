namespace Store.Application.Auditing;

/// <summary>
/// A single audit record to persist. The action filter builds one of these from the request's
/// captured changes plus the caller's identity; domain code (e.g. stock-out, login) builds it
/// explicitly.
/// </summary>
public sealed record AuditEntry
{
    public long? UserId { get; init; }

    public string UserName { get; init; } = "system";

    public string Role { get; init; } = string.Empty;

    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public long? EntityId { get; init; }

    public string? EntityName { get; init; }

    public string? OldValuesJson { get; init; }

    public string? NewValuesJson { get; init; }

    public required string Area { get; init; }

    public string? IpAddress { get; init; }

    public string? CorrelationId { get; init; }
}

/// <summary>Writes append-only audit rows. There is no read/update/delete surface here by design.</summary>
public interface IAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
