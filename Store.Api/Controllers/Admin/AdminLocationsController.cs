using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Country/state lookups for admin forms plus full CRUD (the old Core module's
/// countries and states-provinces admin pages). Country/state <c>Name</c> is bilingual: Arabic in
/// the base column, English in the <c>LocalizedContentProperty</c> overlay. Country keys the overlay
/// by its ISO code (<c>EntityKey</c>); StateOrProvince keys by its numeric id (<c>EntityId</c>).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Settings)]
[Route("api/admin/locations")]
public sealed class AdminLocationsController : ControllerBase
{
    private static readonly string EnCulture = RequestCulture.EnglishCultureId;

    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;
    private readonly ILocalizedContentWriter _localizedWriter;

    public AdminLocationsController(
        StoreDbContext db, ILocalizationService localization, ILocalizedContentWriter localizedWriter)
    {
        _db = db;
        _localization = localization;
        _localizedWriter = localizedWriter;
    }

    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryLookupDto>>> Countries(CancellationToken cancellationToken)
    {
        var countries = await _db.Countries
            .OrderBy(c => c.Name)
            .Select(c => new CountryLookupDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }

    [HttpGet("countries/detail")]
    public async Task<ActionResult<IReadOnlyList<AdminCountryDto>>> CountriesDetail(CancellationToken cancellationToken)
    {
        var countries = await _db.Countries
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Code3, c.IsBillingEnabled, c.IsShippingEnabled,
                c.IsCityEnabled, c.IsZipCodeEnabled, c.IsDistrictEnabled,
                StatesCount = c.StateOrProvinces.Count,
            })
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayByKeyAsync(
            LocalizedEntity.Country, countries.Select(c => c.Id).ToList(), EnCulture, cancellationToken);

        var dtos = countries
            .Select(c => new AdminCountryDto(
                c.Id, c.Name, overlay.Get(c.Id, LocalizedProperty.Name), c.Code3, c.IsBillingEnabled,
                c.IsShippingEnabled, c.IsCityEnabled, c.IsZipCodeEnabled, c.IsDistrictEnabled, c.StatesCount))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("countries")]
    public async Task<ActionResult<AdminCountryDto>> CreateCountry(
        CountryUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            return BadRequest(new { error = "Id (ISO code) is required." });
        }

        if (await _db.Countries.AnyAsync(c => c.Id == request.Id, cancellationToken))
        {
            return Conflict(new { error = $"Country '{request.Id}' already exists." });
        }

        var country = new Country { Id = request.Id };
        Apply(country, request);
        _db.Countries.Add(country);
        await _db.SaveChangesAsync(cancellationToken);

        await _localizedWriter.SetByKeyAsync(
            LocalizedEntity.Country, country.Id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(country, Normalize(request.NameEn), 0));
    }

    [HttpPut("countries/{id}")]
    public async Task<ActionResult<AdminCountryDto>> UpdateCountry(
        string id, CountryUpsertRequest request, CancellationToken cancellationToken)
    {
        var country = await _db.Countries
            .Include(c => c.StateOrProvinces)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (country == null)
        {
            return NotFound();
        }

        Apply(country, request);

        await _localizedWriter.SetByKeyAsync(
            LocalizedEntity.Country, country.Id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(country, Normalize(request.NameEn), country.StateOrProvinces.Count));
    }

    [HttpDelete("countries/{id}")]
    public async Task<IActionResult> DeleteCountry(string id, CancellationToken cancellationToken)
    {
        var country = await _db.Countries.FindAsync([id], cancellationToken);
        if (country == null)
        {
            return NotFound();
        }

        var inUse = await _db.Addresses.AnyAsync(a => a.CountryId == id, cancellationToken)
            || await _db.OrderAddresses.AnyAsync(a => a.CountryId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new { error = "This country is referenced by addresses and cannot be deleted." });
        }

        var states = await _db.StateOrProvinces.Where(s => s.CountryId == id).ToListAsync(cancellationToken);
        foreach (var state in states)
        {
            await _localizedWriter.RemoveAllAsync(LocalizedEntity.StateOrProvince, state.Id, cancellationToken);
        }

        await _localizedWriter.RemoveAllByKeyAsync(LocalizedEntity.Country, id, cancellationToken);

        _db.StateOrProvinces.RemoveRange(states);
        _db.Countries.Remove(country);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("countries/{countryId}/states")]
    public async Task<ActionResult<IReadOnlyList<StateOrProvinceLookupDto>>> States(
        string countryId, CancellationToken cancellationToken)
    {
        var states = await _db.StateOrProvinces
            .Where(s => s.CountryId == countryId)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.CountryId })
            .ToListAsync(cancellationToken);

        var overlay = await _localization.GetOverlayAsync(
            LocalizedEntity.StateOrProvince, states.Select(s => s.Id).ToList(), EnCulture, cancellationToken);

        var dtos = states
            .Select(s => new StateOrProvinceLookupDto(
                s.Id, s.Name, overlay.Get(s.Id, LocalizedProperty.Name), s.CountryId!))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("countries/{countryId}/states")]
    public async Task<ActionResult<StateOrProvinceLookupDto>> CreateState(
        string countryId, StateOrProvinceUpsertRequest request, CancellationToken cancellationToken)
    {
        var countryExists = await _db.Countries.AnyAsync(c => c.Id == countryId, cancellationToken);
        if (!countryExists)
        {
            return NotFound();
        }

        var state = new StateOrProvince
        {
            CountryId = countryId,
            Name = request.Name,
            Code = request.Code,
            Type = request.Type
        };
        _db.StateOrProvinces.Add(state);
        await _db.SaveChangesAsync(cancellationToken);

        await _localizedWriter.SetAsync(
            LocalizedEntity.StateOrProvince, state.Id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new StateOrProvinceLookupDto(state.Id, state.Name, Normalize(request.NameEn), countryId));
    }

    [HttpPut("states/{id:long}")]
    public async Task<ActionResult<StateOrProvinceLookupDto>> UpdateState(
        long id, StateOrProvinceUpsertRequest request, CancellationToken cancellationToken)
    {
        var state = await _db.StateOrProvinces.FindAsync([id], cancellationToken);
        if (state == null)
        {
            return NotFound();
        }

        state.Name = request.Name;
        state.Code = request.Code;
        state.Type = request.Type;

        await _localizedWriter.SetAsync(
            LocalizedEntity.StateOrProvince, state.Id, LocalizedProperty.Name, EnCulture, request.NameEn, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new StateOrProvinceLookupDto(
            state.Id, state.Name, Normalize(request.NameEn), state.CountryId ?? string.Empty));
    }

    [HttpDelete("states/{id:long}")]
    public async Task<IActionResult> DeleteState(long id, CancellationToken cancellationToken)
    {
        var state = await _db.StateOrProvinces.FindAsync([id], cancellationToken);
        if (state == null)
        {
            return NotFound();
        }

        var inUse = await _db.Addresses.AnyAsync(a => a.StateOrProvinceId == id, cancellationToken)
            || await _db.OrderAddresses.AnyAsync(a => a.StateOrProvinceId == id, cancellationToken);
        if (inUse)
        {
            return Conflict(new { error = "This state is referenced by addresses and cannot be deleted." });
        }

        await _localizedWriter.RemoveAllAsync(LocalizedEntity.StateOrProvince, id, cancellationToken);

        _db.StateOrProvinces.Remove(state);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static void Apply(Country country, CountryUpsertRequest request)
    {
        country.Name = request.Name;
        country.Code3 = request.Code3;
        country.IsBillingEnabled = request.IsBillingEnabled;
        country.IsShippingEnabled = request.IsShippingEnabled;
        country.IsCityEnabled = request.IsCityEnabled;
        country.IsZipCodeEnabled = request.IsZipCodeEnabled;
        country.IsDistrictEnabled = request.IsDistrictEnabled;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static AdminCountryDto ToDto(Country c, string? nameEn, int statesCount) => new(
        c.Id, c.Name, nameEn, c.Code3, c.IsBillingEnabled, c.IsShippingEnabled,
        c.IsCityEnabled, c.IsZipCodeEnabled, c.IsDistrictEnabled, statesCount);
}
