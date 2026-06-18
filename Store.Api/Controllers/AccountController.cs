using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Models;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>The signed-in customer's own account (profile read/update).</summary>
[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly TimeProvider _timeProvider;

    public AccountController(UserManager<User> userManager, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    [HttpGet("me")]
    public async Task<ActionResult<AccountProfile>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(await ToProfileAsync(user));
    }

    [HttpPut("me")]
    public async Task<ActionResult<AccountProfile>> Update(UpdateProfileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName;
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        user.LatestUpdatedOn = _timeProvider.GetUtcNow();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        return Ok(await ToProfileAsync(user));
    }

    private async Task<AccountProfile> ToProfileAsync(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        UserName = user.UserName,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Roles = (await _userManager.GetRolesAsync(user)).ToList()
    };
}
