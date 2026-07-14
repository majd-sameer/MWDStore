using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin user management: list/search, create with password, edit profile + roles + customer
/// groups, soft delete. Role changes go through <see cref="UserManager{TUser}"/> so Identity's
/// security stamp stays consistent.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Users)]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditStampReader _auditStamps;

    public AdminUsersController(
        StoreDbContext db, UserManager<User> userManager, TimeProvider timeProvider,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserListItem>>> List(
        [FromQuery] string? query, [FromQuery] string? role, [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        // Staff only — a user with no staff role is a storefront customer and
        // belongs to the customer directory, not this screen (mirrors
        // AdminCustomersController, which selects the complement).
        var users = _db.Users.Where(u => u.Roles.Any(r => AppRoles.Staff.Contains(r.Role.Name!)));
        if (!includeDeleted)
        {
            users = users.Where(u => !u.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            users = users.Where(u => u.Email!.Contains(query) || u.FullName.Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            users = users.Where(u => u.Roles.Any(r => r.Role.Name == role));
        }

        var result = await users
            .OrderByDescending(u => u.Id)
            .Select(u => new AdminUserListItem(
                u.Id, u.Email, u.FullName, u.PhoneNumber, u.CreatedOn, u.IsDeleted,
                u.Roles.Select(r => r.Role.Name!).ToList(),
                u.CustomerGroups.Select(g => g.Name).ToList()))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        var ids = result.Items.Select(u => u.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(User), ids, cancellationToken);
        result = result with
        {
            Items = result.Items
                .Select(u => u with { CreatedBy = stamps.CreatedBy(u.Id), ModifiedBy = stamps.ModifiedBy(u.Id) })
                .ToList()
        };

        return Ok(result);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Roles(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminUserDetail>> Get(long id, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Where(u => u.Id == id && u.Roles.Any(r => AppRoles.Staff.Contains(r.Role.Name!)))
            .Select(u => new AdminUserDetail(
                u.Id, u.Email, u.FullName, u.PhoneNumber,
                u.Roles.Select(r => r.Role.Name!).ToList(),
                u.CustomerGroups.Select(g => g.Id).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDetail>> Create(
        AdminUserCreateRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            UserGuid = Guid.NewGuid(),
            CreatedOn = now,
            LatestUpdatedOn = now
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }

        if (request.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        await SetCustomerGroupsAsync(user.Id, request.CustomerGroupIds, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, await LoadDetailAsync(user.Id, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminUserDetail>> Update(
        long id, AdminUserUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted)
        {
            return NotFound();
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        user.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(request.Roles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToAdd = request.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
        if (rolesToRemove.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        await SetCustomerGroupsAsync(id, request.CustomerGroupIds, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    /// <summary>Soft-deletes the user; sign-in is also locked out.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (id == User.GetUserId())
        {
            return BadRequest(new { error = "You cannot delete your own account." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        user.IsDeleted = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task SetCustomerGroupsAsync(long userId, IList<long> groupIds, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.CustomerGroups)
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var groups = await _db.CustomerGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        user.CustomerGroups.Clear();
        foreach (var group in groups)
        {
            user.CustomerGroups.Add(group);
        }
    }

    private Task<AdminUserDetail?> LoadDetailAsync(long id, CancellationToken cancellationToken) =>
        _db.Users
            .Where(u => u.Id == id)
            .Select(u => new AdminUserDetail(
                u.Id, u.Email, u.FullName, u.PhoneNumber,
                u.Roles.Select(r => r.Role.Name!).ToList(),
                u.CustomerGroups.Select(g => g.Id).ToList()))
            .FirstOrDefaultAsync(cancellationToken)!;
}
