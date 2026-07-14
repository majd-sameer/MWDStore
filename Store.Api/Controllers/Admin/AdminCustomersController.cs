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
/// Admin customer directory: the storefront shoppers (every application user that is
/// <em>not</em> an admin). Mirrors <see cref="AdminUsersController"/> — list/search,
/// create with password, edit profile + customer groups, soft delete — but drops role
/// management (customers hold no roles) and adds per-customer order stats.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Sales)]
[Route("api/admin/customers")]
public sealed class AdminCustomersController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditStampReader _auditStamps;

    public AdminCustomersController(
        StoreDbContext db, UserManager<User> userManager, TimeProvider timeProvider,
        IAuditStampReader auditStamps)
    {
        _db = db;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _auditStamps = auditStamps;
    }

    /// <summary>Customers (non-admin users) with their order count and lifetime spend.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCustomerListItem>>> List(
        [FromQuery] string? query, [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var customers = _db.Users.Where(u => !u.Roles.Any(r => AppRoles.Staff.Contains(r.Role.Name!)));
        if (!includeDeleted)
        {
            customers = customers.Where(u => !u.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            customers = customers.Where(u => u.Email!.Contains(query) || u.FullName.Contains(query));
        }

        var result = await customers
            .OrderByDescending(u => u.Id)
            .Select(u => new AdminCustomerListItem(
                u.Id, u.Email, u.FullName, u.PhoneNumber, u.CreatedOn, u.IsDeleted,
                _db.Orders.Count(o => o.CustomerId == u.Id),
                _db.Orders.Where(o => o.CustomerId == u.Id).Sum(o => (decimal?)o.OrderTotal) ?? 0m,
                u.CustomerGroups.Select(g => g.Name).ToList()))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        var ids = result.Items.Select(c => c.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(User), ids, cancellationToken);
        result = result with
        {
            Items = result.Items
                .Select(c => c with { CreatedBy = stamps.CreatedBy(c.Id), ModifiedBy = stamps.ModifiedBy(c.Id) })
                .ToList()
        };

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminCustomerDetail>> Get(long id, CancellationToken cancellationToken)
    {
        var customer = await _db.Users
            .Where(u => u.Id == id && !u.Roles.Any(r => AppRoles.Staff.Contains(r.Role.Name!)))
            .Select(u => new AdminCustomerDetail(
                u.Id, u.Email, u.FullName, u.PhoneNumber,
                u.CustomerGroups.Select(g => g.Id).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return customer == null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<AdminCustomerDetail>> Create(
        AdminCustomerCreateRequest request, CancellationToken cancellationToken)
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

        await _userManager.AddToRoleAsync(user, AppRoles.Customer);

        await SetCustomerGroupsAsync(user.Id, request.CustomerGroupIds, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, await LoadDetailAsync(user.Id, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminCustomerDetail>> Update(
        long id, AdminCustomerUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.IsDeleted
            || (await _userManager.GetRolesAsync(user)).Any(AppRoles.Staff.Contains))
        {
            return NotFound();
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        user.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _userManager.UpdateAsync(user);

        await SetCustomerGroupsAsync(id, request.CustomerGroupIds, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    /// <summary>Soft-deletes the customer and locks sign-in (matching the user admin).</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null || user.Roles.Any(r => AppRoles.Staff.Contains(r.Role.Name!)))
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

    private Task<AdminCustomerDetail?> LoadDetailAsync(long id, CancellationToken cancellationToken) =>
        _db.Users
            .Where(u => u.Id == id)
            .Select(u => new AdminCustomerDetail(
                u.Id, u.Email, u.FullName, u.PhoneNumber,
                u.CustomerGroups.Select(g => g.Id).ToList()))
            .FirstOrDefaultAsync(cancellationToken)!;
}
