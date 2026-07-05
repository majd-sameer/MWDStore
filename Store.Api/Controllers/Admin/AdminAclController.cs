using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Manages the granular permission ACL: browse the permission catalog, view/create roles, set a role's
/// permissions (stored as role claims), and assign/remove a user's roles. Gated by <c>Acl.Manage</c> — the
/// most sensitive surface, since it governs who can do everything else.
/// </summary>
[ApiController]
[RequirePermission(Permissions.AclManage)]
[Route("api/admin/acl")]
public sealed class AdminAclController : ControllerBase
{
    private readonly RoleManager<Role> _roleManager;
    private readonly UserManager<User> _userManager;

    public AdminAclController(RoleManager<Role> roleManager, UserManager<User> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    /// <summary>The full catalog of assignable permissions.</summary>
    [HttpGet("permissions")]
    public ActionResult<IReadOnlyList<string>> GetPermissions() => Ok(Permissions.All);

    /// <summary>Every role with the permissions currently granted to it.</summary>
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<AclRoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(cancellationToken);

        var result = new List<AclRoleDto>(roles.Count);
        foreach (var role in roles)
        {
            var permissions = await GetRolePermissionsAsync(role);
            result.Add(new AclRoleDto(role.Id, role.Name ?? string.Empty, permissions));
        }

        return Ok(result);
    }

    /// <summary>Creates a new (empty) role. Permissions are assigned separately via SetPermissions.</summary>
    [HttpPost("roles")]
    public async Task<ActionResult<AclRoleDto>> CreateRole(AclCreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "A role name is required." });
        }

        var name = request.Name.Trim();
        if (await _roleManager.RoleExistsAsync(name))
        {
            return Conflict(new { error = $"A role named '{name}' already exists." });
        }

        var result = await _roleManager.CreateAsync(new Role { Name = name });
        if (!result.Succeeded)
        {
            return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }

        var created = await _roleManager.FindByNameAsync(name);
        return Ok(new AclRoleDto(created!.Id, created.Name ?? name, []));
    }

    /// <summary>
    /// Replaces the role's permission set with exactly the supplied permissions. Every value must be a known
    /// permission (unknown values are rejected — fail closed), and the role's own claims are reconciled so the
    /// result matches the request exactly (added missing, removed extras).
    /// </summary>
    [HttpPut("roles/{roleName}/permissions")]
    public async Task<ActionResult<AclRoleDto>> SetPermissions(
        string roleName, AclSetPermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            return NotFound();
        }

        var requested = (request.Permissions ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var unknown = requested.Where(p => !Permissions.IsDefined(p)).ToArray();
        if (unknown.Length > 0)
        {
            return BadRequest(new { error = $"Unknown permission(s): {string.Join(", ", unknown)}." });
        }

        var target = requested.ToHashSet(StringComparer.Ordinal);
        var current = await GetRolePermissionsAsync(role);
        var currentSet = current.ToHashSet(StringComparer.Ordinal);

        foreach (var toRemove in currentSet.Except(target))
        {
            var result = await _roleManager.RemoveClaimAsync(role, new Claim(Permissions.ClaimType, toRemove));
            if (!result.Succeeded)
            {
                return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }
        }

        foreach (var toAdd in target.Except(currentSet))
        {
            var result = await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, toAdd));
            if (!result.Succeeded)
            {
                return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }
        }

        return Ok(new AclRoleDto(role.Id, role.Name ?? roleName, await GetRolePermissionsAsync(role)));
    }

    /// <summary>Adds the user to a role.</summary>
    [HttpPost("users/{userId:long}/roles")]
    public async Task<IActionResult> AssignRole(
        long userId, AclAssignRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Role) || !await _roleManager.RoleExistsAsync(request.Role))
        {
            return BadRequest(new { error = "Unknown role." });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound();
        }

        if (await _userManager.IsInRoleAsync(user, request.Role))
        {
            return NoContent();
        }

        var result = await _userManager.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }

        return NoContent();
    }

    /// <summary>Removes the user from a role.</summary>
    [HttpDelete("users/{userId:long}/roles/{roleName}")]
    public async Task<IActionResult> RemoveRole(long userId, string roleName, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            return NoContent();
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }

        return NoContent();
    }

    private async Task<IReadOnlyList<string>> GetRolePermissionsAsync(Role role)
    {
        var claims = await _roleManager.GetClaimsAsync(role);
        return claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>A role and the permissions granted to it.</summary>
public sealed record AclRoleDto(long Id, string Name, IReadOnlyList<string> Permissions);

public sealed record AclCreateRoleRequest(string Name);

public sealed record AclSetPermissionsRequest(IReadOnlyList<string>? Permissions);

public sealed record AclAssignRoleRequest(string Role);
