using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin app-settings page (old Core module's configuration admin): list + bulk upsert.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Settings)]
[Route("api/admin/settings")]
public sealed class AdminSettingsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminSettingsController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppSettingDto>>> List(CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .OrderBy(s => s.Module).ThenBy(s => s.Id)
            .Select(s => new AppSettingDto(s.Id, s.Value, s.Module, s.IsVisibleInCommonSettingPage))
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    /// <summary>Upserts the given key/value pairs (new keys land in the "Core" module bucket).</summary>
    [HttpPut]
    public async Task<IActionResult> Update(AppSettingUpdateRequest request, CancellationToken cancellationToken)
    {
        var keys = request.Settings.Keys.ToList();
        var existing = await _db.AppSettings
            .Where(s => keys.Contains(s.Id))
            .ToListAsync(cancellationToken);

        foreach (var (key, value) in request.Settings)
        {
            var setting = existing.FirstOrDefault(s => s.Id == key);
            if (setting == null)
            {
                _db.AppSettings.Add(new AppSetting
                {
                    Id = key,
                    Value = value,
                    Module = "Core",
                    IsVisibleInCommonSettingPage = true
                });
            }
            else
            {
                setting.Value = value;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
