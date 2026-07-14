using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Application.Shipping;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin management of shipping providers and the table-rate rows
/// (<see cref="PriceAndDestination"/>) consumed by <see cref="DbShippingPriceService"/>.
/// The standard providers are seeded on first access.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Fulfillment)]
[Route("api/admin/shipping")]
public sealed class AdminShippingController : ControllerBase
{
    private static readonly Expression<Func<PriceAndDestination, AdminTableRateDto>> ToTableRateDto =
        r => new AdminTableRateDto(
            r.Id, r.ShippingProviderId, r.ShippingProvider != null ? r.ShippingProvider.Name : null,
            r.CountryId, r.Country != null ? r.Country.Name : null,
            r.StateOrProvinceId, r.StateOrProvince != null ? r.StateOrProvince.Name : null,
            r.ZipCode, r.MinOrderSubtotal, r.ShippingPrice, r.Note);

    private readonly StoreDbContext _db;
    private readonly IAuditStampReader _auditStamps;

    public AdminShippingController(StoreDbContext db, IAuditStampReader auditStamps)
    {
        _db = db;
        _auditStamps = auditStamps;
    }

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<AdminShippingProviderDto>>> ListProviders(CancellationToken cancellationToken)
    {
        await EnsureSeedProvidersAsync(cancellationToken);

        var providers = await _db.ShippingProviders.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return Ok(providers.Select(ToDto).ToList());
    }

    [HttpPut("providers/{id}")]
    public async Task<ActionResult<AdminShippingProviderDto>> UpdateProvider(
        string id, ShippingProviderUpdateRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.ShippingProviders.FindAsync([id], cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }

        provider.Name = request.Name;
        provider.IsEnabled = request.IsEnabled;
        if (provider.Id == DbShippingPriceService.FreeProviderId)
        {
            provider.AdditionalSettings = JsonSerializer.Serialize(new FreeShippingSetting
            {
                MinimumOrderAmount = request.FreeShippingMinimumOrderAmount ?? 0
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(provider));
    }

    /// <summary>Idempotently ensures the standard providers exist: the two carriers offered at
    /// checkout (Aramex, Jordan Post — enabled, each priced from its own table-rate rows) plus the
    /// legacy Free / generic Table Rate providers (present but disabled).</summary>
    private async Task EnsureSeedProvidersAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.ShippingProviders.Select(p => p.Id).ToListAsync(cancellationToken);
        var seeds = new[]
        {
            new ShippingProvider { Id = DbShippingPriceService.AramexProviderId, Name = "Aramex", IsEnabled = true },
            new ShippingProvider { Id = DbShippingPriceService.JordanPostProviderId, Name = "Jordan Post", IsEnabled = true },
            new ShippingProvider
            {
                Id = DbShippingPriceService.FreeProviderId,
                Name = "Free Shipping",
                IsEnabled = false,
                AdditionalSettings = JsonSerializer.Serialize(new FreeShippingSetting { MinimumOrderAmount = 0 })
            },
            new ShippingProvider { Id = DbShippingPriceService.TableRateProviderId, Name = "Table Rate", IsEnabled = false },
        };

        var added = false;
        foreach (var seed in seeds)
        {
            if (!existing.Contains(seed.Id))
            {
                _db.ShippingProviders.Add(seed);
                added = true;
            }
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static AdminShippingProviderDto ToDto(ShippingProvider provider) => new(
        provider.Id, provider.Name, provider.IsEnabled,
        provider.Id == DbShippingPriceService.FreeProviderId
            ? DbShippingPriceService.ParseFreeShippingSetting(provider.AdditionalSettings).MinimumOrderAmount
            : null);

    [HttpGet("table-rates")]
    public async Task<ActionResult<IReadOnlyList<AdminTableRateDto>>> ListTableRates(
        [FromQuery] string? providerId, CancellationToken cancellationToken = default)
    {
        var query = _db.PriceAndDestinations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            query = query.Where(r => r.ShippingProviderId == providerId);
        }

        var rates = await query
            .OrderBy(r => r.ShippingProviderId).ThenBy(r => r.CountryId).ThenBy(r => r.MinOrderSubtotal)
            .Select(ToTableRateDto)
            .ToListAsync(cancellationToken);

        return Ok(await rates.WithAuditStampsAsync(
            _auditStamps, nameof(PriceAndDestination), r => r.Id,
            (r, createdBy, modifiedBy) => r with { CreatedBy = createdBy, ModifiedBy = modifiedBy },
            cancellationToken));
    }

    [HttpPost("table-rates")]
    public async Task<ActionResult<AdminTableRateDto>> CreateTableRate(
        TableRateUpsertRequest request, CancellationToken cancellationToken)
    {
        var rate = new PriceAndDestination();
        Apply(rate, request);
        _db.PriceAndDestinations.Add(rate);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await GetTableRateDtoAsync(rate.Id, cancellationToken));
    }

    [HttpPut("table-rates/{id:long}")]
    public async Task<ActionResult<AdminTableRateDto>> UpdateTableRate(
        long id, TableRateUpsertRequest request, CancellationToken cancellationToken)
    {
        var rate = await _db.PriceAndDestinations.FindAsync([id], cancellationToken);
        if (rate == null)
        {
            return NotFound();
        }

        Apply(rate, request);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await GetTableRateDtoAsync(id, cancellationToken));
    }

    [HttpDelete("table-rates/{id:long}")]
    public async Task<IActionResult> DeleteTableRate(long id, CancellationToken cancellationToken)
    {
        var rate = await _db.PriceAndDestinations.FindAsync([id], cancellationToken);
        if (rate == null)
        {
            return NotFound();
        }

        _db.PriceAndDestinations.Remove(rate);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(PriceAndDestination rate, TableRateUpsertRequest request)
    {
        rate.ShippingProviderId = string.IsNullOrWhiteSpace(request.ShippingProviderId) ? null : request.ShippingProviderId;
        rate.CountryId = string.IsNullOrWhiteSpace(request.CountryId) ? null : request.CountryId;
        rate.StateOrProvinceId = request.StateOrProvinceId;
        rate.ZipCode = string.IsNullOrWhiteSpace(request.ZipCode) ? null : request.ZipCode;
        rate.MinOrderSubtotal = request.MinOrderSubtotal;
        rate.ShippingPrice = request.ShippingPrice;
        rate.Note = request.Note;
    }

    private Task<AdminTableRateDto?> GetTableRateDtoAsync(long id, CancellationToken cancellationToken) =>
        _db.PriceAndDestinations
            .Where(r => r.Id == id)
            .Select(ToTableRateDto)
            .FirstOrDefaultAsync(cancellationToken)!;
}
