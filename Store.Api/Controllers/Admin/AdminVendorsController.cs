using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for vendors (old Vendors module). Deletes are soft.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Settings)]
[Route("api/admin/vendors")]
public sealed class AdminVendorsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdminVendorsController(StoreDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminVendorDto>>> List(CancellationToken cancellationToken)
    {
        var vendors = await _db.Vendors
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.Name)
            .Select(v => new AdminVendorDto(v.Id, v.Name, v.Slug, v.Email, v.Description, v.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(vendors);
    }

    [HttpPost]
    public async Task<ActionResult<AdminVendorDto>> Create(VendorUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var vendor = new Vendor { CreatedOn = now, LatestUpdatedOn = now };
        Apply(vendor, request);
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(vendor));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminVendorDto>> Update(
        long id, VendorUpsertRequest request, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor == null)
        {
            return NotFound();
        }

        Apply(vendor, request);
        vendor.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(vendor));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor == null)
        {
            return NotFound();
        }

        vendor.IsDeleted = true;
        vendor.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(Vendor vendor, VendorUpsertRequest request)
    {
        vendor.Name = request.Name;
        vendor.Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug.Generate(request.Name) : request.Slug;
        vendor.Email = request.Email;
        vendor.Description = request.Description;
        vendor.IsActive = request.IsActive;
    }

    private static AdminVendorDto ToDto(Vendor v) => new(v.Id, v.Name, v.Slug, v.Email, v.Description, v.IsActive);
}
