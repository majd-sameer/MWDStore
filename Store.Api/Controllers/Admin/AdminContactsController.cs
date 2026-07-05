using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>Admin view of contact submissions + contact-area management (old Contacts module).</summary>
[ApiController]
[RequirePermission(Permissions.ContentManage)]
[Route("api/admin/contacts")]
public sealed class AdminContactsController : ControllerBase
{
    private readonly StoreDbContext _db;

    public AdminContactsController(StoreDbContext db)
    {
        _db = db;
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

    // ----- Areas ------------------------------------------------------------------------------------

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
