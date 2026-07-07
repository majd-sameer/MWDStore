namespace Store.Application.Auditing;

/// <summary>
/// The identity/context of whoever triggered a domain-level audited action, captured by the API
/// controller from the JWT + request and handed to services that log audit rows themselves (e.g. the
/// stock-out service). Keeps HTTP concerns out of the application layer while still recording the
/// full actor snapshot.
/// </summary>
public sealed record AuditActor(
    long? UserId,
    string UserName,
    string Role,
    string? IpAddress,
    string? CorrelationId)
{
    public static readonly AuditActor System = new(null, "system", string.Empty, null, null);
}
