using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
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
    public async Task<ActionResult<PagedResult<AuditLogListItem>>> List(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] long? userId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string[]? actions = null,
        [FromQuery] string[]? areas = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
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

        if (actions is { Length: > 0 })
        {
            logs = logs.Where(l => actions.Contains(l.Action));
        }

        if (areas is { Length: > 0 })
        {
            logs = logs.Where(l => areas.Contains(l.Area));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            logs = logs.Where(l =>
                (l.EntityName != null && l.EntityName.Contains(term)) || l.UserName.Contains(term));
        }

        var result = await logs
            .OrderByDescending(l => l.CreatedOn)
            .ThenByDescending(l => l.Id)
            .Select(l => new AuditLogListItem(
                l.Id, l.CreatedOn, l.UserId, l.UserName, l.Role, l.Action,
                l.EntityType, l.EntityId, l.EntityName, l.Area))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        return Ok(result);
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
