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

    /// <param name="countryId">ISO code of the country whose states are listed.</param>
    /// <param name="withRatesOnly">When true, only states an enabled shipping provider has a
    /// table-rate (<see cref="Store.Domain.PriceAndDestination"/>) row for — the checkout uses this so
    /// shoppers can only pick a governorate that can actually be shipped to. A state-wildcard rate row
    /// (null <c>StateOrProvinceId</c>) covers every state, so no filtering happens then.</param>
    /// <param name="cancellationToken">Aborts the lookups when the request is cancelled.</param>
    [HttpGet("countries/{countryId}/states")]
    public async Task<ActionResult<IReadOnlyList<StateOrProvinceLookupDto>>> States(
        string countryId, [FromQuery] bool withRatesOnly, CancellationToken cancellationToken)
    {
        var statesQuery = _db.StateOrProvinces.Where(s => s.CountryId == countryId);

        if (withRatesOnly)
        {
            var rateRows = _db.PriceAndDestinations.Where(r =>
                (r.CountryId == null || r.CountryId == countryId)
                && _db.ShippingProviders.Any(p => p.IsEnabled && p.Id == r.ShippingProviderId));

            var hasWildcard = await rateRows.AnyAsync(r => r.StateOrProvinceId == null, cancellationToken);
            if (!hasWildcard)
            {
                statesQuery = statesQuery.Where(s => rateRows.Any(r => r.StateOrProvinceId == s.Id));
            }
        }

        var states = await statesQuery
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
                // Re-sort after overlaying so the English list is alphabetical too (base sort is Arabic).
                states = states
                    .Select(s => s with { Name = overlay.Apply(s.Id, LocalizedProperty.Name, s.Name)! })
                    .OrderBy(s => s.Name, StringComparer.Ordinal)
                    .ToList();
            }
        }

        return Ok(states);
    }
}
