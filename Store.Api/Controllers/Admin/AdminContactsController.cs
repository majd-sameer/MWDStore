using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin view of contact submissions + contact-area management.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Sales)]
[Route("api/admin/contacts")]
public sealed class AdminContactsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IAuditStampReader _auditStamps;

    public AdminContactsController(StoreDbContext db, IAuditStampReader auditStamps)
    {
        _db = db;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminContactDto>>> List(CancellationToken cancellationToken)
    {
        var contacts = await _db.Contacts
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.Id)
            .Take(200)
            .Select(c => new AdminContactDto(
                c.Id, c.FullName, c.EmailAddress, c.PhoneNumber, c.Address, c.Content,
                c.ContactAreaId, c.ContactArea.Name, c.CreatedOn))
            .ToListAsync(cancellationToken);

        var ids = contacts.Select(x => x.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(Contact), ids, cancellationToken);
        contacts = contacts
            .Select(x => x with { CreatedBy = stamps.CreatedBy(x.Id), ModifiedBy = stamps.ModifiedBy(x.Id) })
            .ToList();

        return Ok(contacts);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (contact == null)
        {
            return NotFound();
        }

        contact.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AdminContactAreaDto>>> Areas(CancellationToken cancellationToken)
    {
        var areas = await _db.ContactAreas
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Name)
            .Select(a => new AdminContactAreaDto(a.Id, a.Name))
            .ToListAsync(cancellationToken);

        return Ok(areas);
    }

    [HttpPost("areas")]
    public async Task<ActionResult<AdminContactAreaDto>> CreateArea(
        ContactAreaUpsertRequest request, CancellationToken cancellationToken)
    {
        var area = new ContactArea { Name = request.Name };
        _db.ContactAreas.Add(area);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminContactAreaDto(area.Id, area.Name));
    }

    [HttpPut("areas/{id:long}")]
    public async Task<ActionResult<AdminContactAreaDto>> UpdateArea(
        long id, ContactAreaUpsertRequest request, CancellationToken cancellationToken)
    {
        var area = await _db.ContactAreas.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
        if (area == null)
        {
            return NotFound();
        }

        area.Name = request.Name;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminContactAreaDto(area.Id, area.Name));
    }

    [HttpDelete("areas/{id:long}")]
    public async Task<IActionResult> DeleteArea(long id, CancellationToken cancellationToken)
    {
        var area = await _db.ContactAreas.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
        if (area == null)
        {
            return NotFound();
        }

        area.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
