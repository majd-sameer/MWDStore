using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>Read-only system logs: activity log (old ActivityLog module) and the search-query
/// log (old Search module's admin page, grouped by query text).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Settings)]
[Route("api/admin/logs")]
public sealed class AdminSystemLogsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminSystemLogsController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("activities")]
    public async Task<ActionResult<IReadOnlyList<AdminActivityDto>>> Activities(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var activities = await _db.Activities
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AdminActivityDto(
                a.Id, a.ActivityTypeId, a.ActivityType.Name, a.UserId, a.EntityId, a.EntityTypeId, a.CreatedOn))
            .ToListAsync(cancellationToken);

        return Ok(activities);
    }

    [HttpGet("search-queries")]
    public async Task<ActionResult<IReadOnlyList<AdminSearchQueryDto>>> SearchQueries(CancellationToken cancellationToken)
    {
        var queries = await _db.Queries
            .GroupBy(q => q.QueryText)
            .Select(g => new AdminSearchQueryDto(g.Key, g.Count(), g.Max(q => q.CreatedOn)))
            .OrderByDescending(q => q.Count)
            .Take(200)
            .ToListAsync(cancellationToken);

        return Ok(queries);
    }
}
