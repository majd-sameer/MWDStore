using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin localization (old Localization module): cultures and per-culture resource strings.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Settings)]
[Route("api/admin/localization")]
public sealed class AdminLocalizationController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminLocalizationController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("cultures")]
    public async Task<ActionResult<IReadOnlyList<CultureDto>>> Cultures(CancellationToken cancellationToken)
    {
        var cultures = await _db.Cultures
            .OrderBy(c => c.Id)
            .Select(c => new CultureDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        return Ok(cultures);
    }

    [HttpPost("cultures")]
    public async Task<ActionResult<CultureDto>> CreateCulture(CultureDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Id and Name are required." });
        }

        if (await _db.Cultures.AnyAsync(c => c.Id == request.Id, cancellationToken))
        {
            return Conflict(new { error = $"Culture '{request.Id}' already exists." });
        }

        _db.Cultures.Add(new Culture { Id = request.Id, Name = request.Name });
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(request);
    }

    [HttpGet("resources")]
    public async Task<ActionResult<IReadOnlyList<AdminResourceDto>>> Resources(
        [FromQuery] string cultureId, [FromQuery] string? query, CancellationToken cancellationToken)
    {
        var resources = _db.Resources.Where(r => r.CultureId == cultureId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            resources = resources.Where(r => r.Key.Contains(query) || (r.Value != null && r.Value.Contains(query)));
        }

        var items = await resources
            .OrderBy(r => r.Key)
            .Take(500)
            .Select(r => new AdminResourceDto(r.Id, r.Key, r.Value, r.CultureId))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Upserts a resource string by (culture, key).</summary>
    [HttpPost("resources")]
    public async Task<ActionResult<AdminResourceDto>> UpsertResource(
        ResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        var cultureExists = await _db.Cultures.AnyAsync(c => c.Id == request.CultureId, cancellationToken);
        if (!cultureExists)
        {
            return BadRequest(new { error = $"Culture '{request.CultureId}' does not exist." });
        }

        var resource = await _db.Resources
            .FirstOrDefaultAsync(r => r.CultureId == request.CultureId && r.Key == request.Key, cancellationToken);
        if (resource == null)
        {
            resource = new Resource { CultureId = request.CultureId, Key = request.Key };
            _db.Resources.Add(resource);
        }

        resource.Value = request.Value;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminResourceDto(resource.Id, resource.Key, resource.Value, resource.CultureId));
    }

    [HttpDelete("resources/{id:long}")]
    public async Task<IActionResult> DeleteResource(long id, CancellationToken cancellationToken)
    {
        var resource = await _db.Resources.FindAsync([id], cancellationToken);
        if (resource == null)
        {
            return NotFound();
        }

        _db.Resources.Remove(resource);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
