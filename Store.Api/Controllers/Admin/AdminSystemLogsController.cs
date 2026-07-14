using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>Read-only system logs: activity log and the search-query log (grouped by query text).</summary>
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
    public async Task<ActionResult<PagedResult<AdminActivityDto>>> Activities(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var activities = await _db.Activities
            .OrderByDescending(a => a.Id)
            .Select(a => new AdminActivityDto(
                a.Id, a.ActivityTypeId, a.ActivityType.Name, a.UserId, a.EntityId, a.EntityTypeId, a.CreatedOn))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

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
