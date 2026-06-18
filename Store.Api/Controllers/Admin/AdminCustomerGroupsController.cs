using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for customer groups (used by cart/catalog rule targeting). Deletes are soft.</summary>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/customer-groups")]
public sealed class AdminCustomerGroupsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdminCustomerGroupsController(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCustomerGroupDto>>> List(CancellationToken cancellationToken)
    {
        var groups = await _db.CustomerGroups
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Name)
            .Select(g => new AdminCustomerGroupDto(g.Id, g.Name, g.Description, g.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }

    [HttpPost]
    public async Task<ActionResult<AdminCustomerGroupDto>> Create(
        CustomerGroupUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var group = new CustomerGroup
        {
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedOn = now,
            LatestUpdatedOn = now
        };
        _db.CustomerGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminCustomerGroupDto(group.Id, group.Name, group.Description, group.IsActive));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminCustomerGroupDto>> Update(
        long id, CustomerGroupUpsertRequest request, CancellationToken cancellationToken)
    {
        var group = await _db.CustomerGroups.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, cancellationToken);
        if (group == null)
        {
            return NotFound();
        }

        group.Name = request.Name;
        group.Description = request.Description;
        group.IsActive = request.IsActive;
        group.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminCustomerGroupDto(group.Id, group.Name, group.Description, group.IsActive));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var group = await _db.CustomerGroups.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, cancellationToken);
        if (group == null)
        {
            return NotFound();
        }

        group.IsDeleted = true;
        group.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
