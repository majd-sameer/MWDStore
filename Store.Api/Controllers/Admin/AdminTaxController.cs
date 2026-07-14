using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin CRUD for tax classes and tax rates (consumed by checkout's <c>TaxService</c>).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Taxes)]
[Route("api/admin/tax")]
public sealed class AdminTaxController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IAuditStampReader _auditStamps;

    public AdminTaxController(StoreDbContext db, IAuditStampReader auditStamps)
    {
        _db = db;
        _auditStamps = auditStamps;
    }

    [HttpGet("classes")]
    public async Task<ActionResult<IReadOnlyList<AdminTaxClassDto>>> ListClasses(CancellationToken cancellationToken)
    {
        var classes = await _db.TaxClasses
            .OrderBy(c => c.Name)
            .Select(c => new AdminTaxClassDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var ids = classes.Select(c => c.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(TaxClass), ids, cancellationToken);
        classes = classes
            .Select(c => c with { CreatedBy = stamps.CreatedBy(c.Id), ModifiedBy = stamps.ModifiedBy(c.Id) })
            .ToList();

        return Ok(classes);
    }

    [HttpPost("classes")]
    public async Task<ActionResult<AdminTaxClassDto>> CreateClass(
        TaxClassUpsertRequest request, CancellationToken cancellationToken)
    {
        var taxClass = new TaxClass { Name = request.Name };
        _db.TaxClasses.Add(taxClass);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminTaxClassDto(taxClass.Id, taxClass.Name));
    }

    [HttpPut("classes/{id:long}")]
    public async Task<ActionResult<AdminTaxClassDto>> UpdateClass(
        long id, TaxClassUpsertRequest request, CancellationToken cancellationToken)
    {
        var taxClass = await _db.TaxClasses.FindAsync([id], cancellationToken);
        if (taxClass == null)
        {
            return NotFound();
        }

        taxClass.Name = request.Name;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminTaxClassDto(taxClass.Id, taxClass.Name));
    }

    [HttpDelete("classes/{id:long}")]
    public async Task<IActionResult> DeleteClass(long id, CancellationToken cancellationToken)
    {
        var taxClass = await _db.TaxClasses.FindAsync([id], cancellationToken);
        if (taxClass == null)
        {
            return NotFound();
        }

        var inUse = await _db.Products.AnyAsync(p => p.TaxClassId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new { error = "This tax class is assigned to products and cannot be deleted." });
        }

        var rates = await _db.TaxRates.Where(r => r.TaxClassId == id).ToListAsync(cancellationToken);
        _db.TaxRates.RemoveRange(rates);
        _db.TaxClasses.Remove(taxClass);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("rates")]
    public async Task<ActionResult<IReadOnlyList<AdminTaxRateDto>>> ListRates(CancellationToken cancellationToken)
    {
        var rates = await _db.TaxRates
            .OrderBy(r => r.TaxClass.Name).ThenBy(r => r.CountryId)
            .Select(r => new AdminTaxRateDto(
                r.Id, r.TaxClassId, r.TaxClass.Name, r.CountryId,
                r.Country != null ? r.Country.Name : null,
                r.StateOrProvinceId, r.StateOrProvince != null ? r.StateOrProvince.Name : null,
                r.ZipCode, r.Rate))
            .ToListAsync(cancellationToken);

        var ids = rates.Select(r => r.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(TaxRate), ids, cancellationToken);
        rates = rates
            .Select(r => r with { CreatedBy = stamps.CreatedBy(r.Id), ModifiedBy = stamps.ModifiedBy(r.Id) })
            .ToList();

        return Ok(rates);
    }

    [HttpPost("rates")]
    public async Task<ActionResult<AdminTaxRateDto>> CreateRate(
        TaxRateUpsertRequest request, CancellationToken cancellationToken)
    {
        var rate = new TaxRate();
        Apply(rate, request);
        _db.TaxRates.Add(rate);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await GetRateDtoAsync(rate.Id, cancellationToken));
    }

    [HttpPut("rates/{id:long}")]
    public async Task<ActionResult<AdminTaxRateDto>> UpdateRate(
        long id, TaxRateUpsertRequest request, CancellationToken cancellationToken)
    {
        var rate = await _db.TaxRates.FindAsync([id], cancellationToken);
        if (rate == null)
        {
            return NotFound();
        }

        Apply(rate, request);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await GetRateDtoAsync(id, cancellationToken));
    }

    [HttpDelete("rates/{id:long}")]
    public async Task<IActionResult> DeleteRate(long id, CancellationToken cancellationToken)
    {
        var rate = await _db.TaxRates.FindAsync([id], cancellationToken);
        if (rate == null)
        {
            return NotFound();
        }

        _db.TaxRates.Remove(rate);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(TaxRate rate, TaxRateUpsertRequest request)
    {
        rate.TaxClassId = request.TaxClassId;
        rate.CountryId = string.IsNullOrWhiteSpace(request.CountryId) ? null : request.CountryId;
        rate.StateOrProvinceId = request.StateOrProvinceId;
        rate.ZipCode = string.IsNullOrWhiteSpace(request.ZipCode) ? null : request.ZipCode;
        rate.Rate = request.Rate;
    }

    private Task<AdminTaxRateDto?> GetRateDtoAsync(long id, CancellationToken cancellationToken) =>
        _db.TaxRates
            .Where(r => r.Id == id)
            .Select(r => new AdminTaxRateDto(
                r.Id, r.TaxClassId, r.TaxClass.Name, r.CountryId,
                r.Country != null ? r.Country.Name : null,
                r.StateOrProvinceId, r.StateOrProvince != null ? r.StateOrProvince.Name : null,
                r.ZipCode, r.Rate))
            .FirstOrDefaultAsync(cancellationToken)!;
}
