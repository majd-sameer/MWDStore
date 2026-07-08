using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Localization;
using Store.Data;

namespace Store.Api.Controllers;

/// <summary>
/// Public country/state lookups for storefront address forms (checkout, account addresses).
/// Read-only and anonymous; only shipping-enabled countries are listed. Names are overlaid with the
/// English translation when the request asks for English. Full CRUD lives in
/// <see cref="Admin.AdminLocationsController"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/locations")]
public sealed class LocationsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly ILocalizationService _localization;

    public LocationsController(StoreDbContext db, ILocalizationService localization)
    {
        _db = db;
        _localization = localization;
    }

    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryLookupDto>>> Countries(CancellationToken cancellationToken)
    {
        var countries = await _db.Countries
            .Where(c => c.IsShippingEnabled)
            .OrderBy(c => c.Name)
            .Select(c => new CountryLookupDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        if (cultureId is not null && countries.Count > 0)
        {
            var overlay = await _localization.GetOverlayByKeyAsync(
                LocalizedEntity.Country, countries.Select(c => c.Id).ToList(), cultureId, cancellationToken);
            if (!overlay.IsEmpty)
            {
                countries = countries
                    .Select(c => c with { Name = overlay.Apply(c.Id, LocalizedProperty.Name, c.Name)! })
                    .ToList();
            }
        }

        return Ok(countries);
    }

    [HttpGet("countries/{countryId}/states")]
    public async Task<ActionResult<IReadOnlyList<StateOrProvinceLookupDto>>> States(
        string countryId, CancellationToken cancellationToken)
    {
        var states = await _db.StateOrProvinces
            .Where(s => s.CountryId == countryId)
            .OrderBy(s => s.Name)
            .Select(s => new StateOrProvinceLookupDto(s.Id, s.Name, null, s.CountryId!))
            .ToListAsync(cancellationToken);

        var cultureId = RequestCulture.OverlayCultureId(Request);
        if (cultureId is not null && states.Count > 0)
        {
            var overlay = await _localization.GetOverlayAsync(
                LocalizedEntity.StateOrProvince, states.Select(s => s.Id).ToList(), cultureId, cancellationToken);
            if (!overlay.IsEmpty)
            {
                states = states
                    .Select(s => s with { Name = overlay.Apply(s.Id, LocalizedProperty.Name, s.Name)! })
                    .ToList();
            }
        }

        return Ok(states);
    }
}
