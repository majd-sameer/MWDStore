using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Auditing;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Comment moderation, the port of the old Comments admin. Status values follow the old enum:
/// 1 = Pending, 5 = Approved, 8 = NotApproved.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Moderation)]
[Route("api/admin/comments")]
public sealed class AdminCommentsController : ControllerBase
{
    private static readonly int[] ValidStatuses = [1, 5, 8];

    private readonly StoreDbContext _db;
    private readonly IAuditStampReader _auditStamps;

    public AdminCommentsController(StoreDbContext db, IAuditStampReader auditStamps)
    {
        _db = db;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCommentDto>>> List(
        [FromQuery] int[]? statuses, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var comments = _db.Comments.AsQueryable();
        if (statuses is { Length: > 0 })
        {
            comments = comments.Where(c => statuses.Contains(c.Status));
        }

        var result = await comments
            .OrderByDescending(c => c.Id)
            .Select(c => new AdminCommentDto(
                c.Id, c.CommentText, c.CommenterName, c.User.Email,
                c.Status, c.CreatedOn, c.EntityId, c.EntityTypeId, c.ParentId))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        var ids = result.Items.Select(x => x.Id).ToList();
        var stamps = await _auditStamps.ReadAsync(nameof(Comment), ids, cancellationToken);
        result = result with
        {
            Items = result.Items
                .Select(x => x with { CreatedBy = stamps.CreatedBy(x.Id), ModifiedBy = stamps.ModifiedBy(x.Id) })
                .ToList(),
        };

        return Ok(result);
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long id, ModerationStatusRequest request, CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(request.Status))
        {
            return BadRequest(new { error = "Status must be 1 (Pending), 5 (Approved) or 8 (NotApproved)." });
        }

        var comment = await _db.Comments.FindAsync([id], cancellationToken);
        if (comment == null)
        {
            return NotFound();
        }

        comment.Status = request.Status;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var comment = await _db.Comments
            .Include(c => c.InverseParent)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (comment == null)
        {
            return NotFound();
        }

        _db.Comments.RemoveRange(comment.InverseParent);
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
