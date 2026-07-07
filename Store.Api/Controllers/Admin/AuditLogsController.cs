using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Read-only view over the append-only audit trail. Gated to the Settings area (super-admin/admin),
/// matching the access matrix. There is intentionally no create/update/delete surface — rows are
/// written only by <see cref="AuditActionFilter"/> and explicit domain events.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Settings)]
[Route("api/admin/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AuditLogsController(StoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogListItem>>> List(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] long? userId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] string? area = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var logs = _db.AuditLogs.AsNoTracking();

        if (from is { } fromValue)
        {
            logs = logs.Where(l => l.CreatedOn >= fromValue);
        }

        if (to is { } toValue)
        {
            logs = logs.Where(l => l.CreatedOn <= toValue);
        }

        if (userId is { } uid)
        {
            logs = logs.Where(l => l.UserId == uid);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            logs = logs.Where(l => l.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            logs = logs.Where(l => l.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            logs = logs.Where(l => l.Area == area);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            logs = logs.Where(l =>
                (l.EntityName != null && l.EntityName.Contains(term)) || l.UserName.Contains(term));
        }

        var items = await logs
            .OrderByDescending(l => l.CreatedOn)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogListItem(
                l.Id, l.CreatedOn, l.UserId, l.UserName, l.Role, l.Action,
                l.EntityType, l.EntityId, l.EntityName, l.Area))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuditLogDetail>> Get(long id, CancellationToken cancellationToken)
    {
        var log = await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new AuditLogDetail(
                l.Id, l.CreatedOn, l.UserId, l.UserName, l.Role, l.Action,
                l.EntityType, l.EntityId, l.EntityName, l.Area,
                l.IpAddress, l.CorrelationId, l.OldValuesJson, l.NewValuesJson))
            .FirstOrDefaultAsync(cancellationToken);

        return log is null ? NotFound() : Ok(log);
    }
}

public sealed record AuditLogListItem(
    long Id, DateTime CreatedOn, long? UserId, string UserName, string Role, string Action,
    string EntityType, long? EntityId, string? EntityName, string Area);

public sealed record AuditLogDetail(
    long Id, DateTime CreatedOn, long? UserId, string UserName, string Role, string Action,
    string EntityType, long? EntityId, string? EntityName, string Area,
    string? IpAddress, string? CorrelationId, string? OldValuesJson, string? NewValuesJson);
