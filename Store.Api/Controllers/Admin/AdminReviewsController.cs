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
/// Review moderation. Status values: 1 = Pending, 5 = Approved, 8 = NotApproved.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Moderation)]
[Route("api/admin/reviews")]
public sealed class AdminReviewsController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly IAuditStampReader _auditStamps;

    public AdminReviewsController(StoreDbContext db, IAuditStampReader auditStamps)
    {
        _db = db;
        _auditStamps = auditStamps;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminReviewDto>>> List(
        [FromQuery] int[]? statuses, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var reviews = _db.Reviews.AsQueryable();
        if (statuses is { Length: > 0 })
        {
            reviews = reviews.Where(r => statuses.Contains(r.Status));
        }

        var result = await reviews
            .OrderByDescending(r => r.Id)
            .Select(r => new AdminReviewDto(
                r.Id, r.Title, r.Comment, r.Rating, r.ReviewerName, r.User.Email,
                r.Status, r.CreatedOn, r.EntityId, r.EntityTypeId,
                _db.Products.Where(p => p.Id == r.EntityId).Select(p => p.Name).FirstOrDefault()))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        return Ok(await result.WithAuditStampsAsync(
            _auditStamps, nameof(Review), x => x.Id,
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

        var review = await _db.Reviews.FindAsync([id], cancellationToken);
        if (review == null)
        {
            return NotFound();
        }

        review.Status = request.Status;
        await RecalculateProductRatingAsync(
            review.EntityId, review.Id, request.Status == Moderation.Approved ? review.Rating : null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var review = await _db.Reviews.Include(r => r.Replies).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (review == null)
        {
            return NotFound();
        }

        _db.Replies.RemoveRange(review.Replies);
        _db.Reviews.Remove(review);
        await RecalculateProductRatingAsync(review.EntityId, review.Id, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Keeps the product's denormalized ReviewsCount/RatingAverage in sync with approved reviews.
    /// The changed review is excluded from the DB query (its stored row is stale at this point)
    /// and re-added via <paramref name="changedApprovedRating"/> when it ends up approved.
    /// </summary>
    private async Task RecalculateProductRatingAsync(
        long productId, long changedReviewId, int? changedApprovedRating, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product == null)
        {
            return;
        }

        var ratings = await _db.Reviews
            .Where(r => r.EntityId == productId && r.Status == Moderation.Approved && r.Id != changedReviewId)
            .Select(r => (double)r.Rating)
            .ToListAsync(cancellationToken);

        if (changedApprovedRating.HasValue)
        {
            ratings.Add(changedApprovedRating.Value);
        }

        product.ReviewsCount = ratings.Count;
        product.RatingAverage = ratings.Count > 0 ? ratings.Average() : null;
    }
}
