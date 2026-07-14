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
/// Comment moderation. Status values: 1 = Pending, 5 = Approved, 8 = NotApproved.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Moderation)]
[Route("api/admin/comments")]
public sealed class AdminCommentsController : ControllerBase
{
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

        return Ok(await result.WithAuditStampsAsync(
            _auditStamps, nameof(Comment), x => x.Id,
            (x, createdBy, modifiedBy) => x with { CreatedBy = createdBy, ModifiedBy = modifiedBy },
            cancellationToken));
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long id, ModerationStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Moderation.ValidStatuses.Contains(request.Status))
        {
            return BadRequest(new { error = Moderation.InvalidStatusError });
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
