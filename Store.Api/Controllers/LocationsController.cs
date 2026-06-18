using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Models;
using Store.Data;

namespace Store.Api.Controllers;

/// <summary>
/// Public country/state lookups for storefront address forms (checkout, account addresses).
/// Read-only and anonymous; only shipping-enabled countries are listed. Full CRUD lives in
/// <see cref="Admin.AdminLocationsController"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/locations")]
public sealed class LocationsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public LocationsController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("countries")]
    public async Task<ActionResult<IReadOnlyList<CountryLookupDto>>> Countries(CancellationToken cancellationToken)
    {
        var countries = await _db.Countries
            .Where(c => c.IsShippingEnabled)
            .OrderBy(c => c.Name)
            .Select(c => new CountryLookupDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }

    [HttpGet("countries/{countryId}/states")]
    public async Task<ActionResult<IReadOnlyList<StateOrProvinceLookupDto>>> States(
        string countryId, CancellationToken cancellationToken)
    {
        var states = await _db.StateOrProvinces
            .Where(s => s.CountryId == countryId)
            .OrderBy(s => s.Name)
            .Select(s => new StateOrProvinceLookupDto(s.Id, s.Name, s.CountryId!))
            .ToListAsync(cancellationToken);

        return Ok(states);
    }
}
