using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Admin payment management: the provider list with per-gateway settings (the old Payments module's
/// provider page + each gateway's config page, generalized to a JSON settings blob) and the payment
/// transaction log. The standard providers are seeded on first access using the old modules' ids.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Payments)]
[Route("api/admin/payments")]
public sealed class AdminPaymentsController : ControllerBase
{
    private static readonly (string Id, string Name, bool Enabled)[] StandardProviders =
    [
        ("CoD", "Cash On Delivery", true),
        ("Stripe", "Stripe", false),
        ("Braintree", "Braintree", false),
        ("PaypalExpress", "Paypal Express", false),
        ("MEPS", "MEPS (Middle East Payment Services)", false)
    ];

    private readonly StoreDbContext _db;

    public AdminPaymentsController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<AdminPaymentProviderDto>>> Providers(CancellationToken cancellationToken)
    {
        // Backfill any standard providers missing from the table so newly added gateways
        // (e.g. MEPS) appear even after the table was first seeded.
        var existingIds = await _db.PaymentProviders
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var missing = StandardProviders.Where(p => !existingIds.Contains(p.Id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var (id, name, enabled) in missing)
            {
                _db.PaymentProviders.Add(new PaymentProvider { Id = id, Name = name, IsEnabled = enabled });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var providers = await _db.PaymentProviders
            .OrderBy(p => p.Name)
            .Select(p => new AdminPaymentProviderDto(p.Id, p.Name, p.IsEnabled, p.AdditionalSettings))
            .ToListAsync(cancellationToken);

        return Ok(providers);
    }

    [HttpPut("providers/{id}")]
    public async Task<ActionResult<AdminPaymentProviderDto>> UpdateProvider(
        string id, PaymentProviderUpdateRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.PaymentProviders.FindAsync([id], cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalSettings))
        {
            try
            {
                JsonDocument.Parse(request.AdditionalSettings).Dispose();
            }
            catch (JsonException)
            {
                return BadRequest(new { error = "AdditionalSettings must be valid JSON." });
            }
        }

        provider.Name = request.Name;
        provider.IsEnabled = request.IsEnabled;
        provider.AdditionalSettings = string.IsNullOrWhiteSpace(request.AdditionalSettings)
            ? null
            : request.AdditionalSettings;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminPaymentProviderDto(provider.Id, provider.Name, provider.IsEnabled, provider.AdditionalSettings));
    }

    /// <summary>Payment transaction log (newest first).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminPaymentDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _db.Payments
            .OrderByDescending(p => p.Id)
            .Select(p => new AdminPaymentDto(
                p.Id, p.OrderId, p.Amount, p.PaymentFee, p.PaymentMethod,
                p.GatewayTransactionId, p.Status, p.CreatedOn))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        return Ok(result);
    }
}
