using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Data;

namespace Store.Api.Controllers.Admin;

/// <summary>
/// Review moderation, the port of the old Reviews admin. Status values follow the old enum:
/// 1 = Pending, 5 = Approved, 8 = NotApproved.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.Moderation)]
[Route("api/admin/reviews")]
public sealed class AdminReviewsController : ControllerBase
{
    private static readonly int[] ValidStatuses = [1, 5, 8];

    private readonly StoreDbContext _db;

    public AdminReviewsController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminReviewDto>>> List(
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var reviews = _db.Reviews.AsQueryable();
        if (status.HasValue)
        {
            reviews = reviews.Where(r => r.Status == status.Value);
        }

        var items = await reviews
            .OrderByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new AdminReviewDto(
                r.Id, r.Title, r.Comment, r.Rating, r.ReviewerName, r.User.Email,
                r.Status, r.CreatedOn, r.EntityId, r.EntityTypeId,
                _db.Products.Where(p => p.Id == r.EntityId).Select(p => p.Name).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long id, ModerationStatusRequest request, CancellationToken cancellationToken)
    {
        if (!ValidStatuses.Contains(request.Status))
        {
            return BadRequest(new { error = "Status must be 1 (Pending), 5 (Approved) or 8 (NotApproved)." });
        }

        var review = await _db.Reviews.FindAsync([id], cancellationToken);
        if (review == null)
        {
            return NotFound();
        }

        review.Status = request.Status;
        await RecalculateProductRatingAsync(
            review.EntityId, review.Id, request.Status == 5 ? review.Rating : null, cancellationToken);
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
    /// Keeps the product's denormalized ReviewsCount/RatingAverage in sync with approved reviews,
    /// like the old module did on approval. The changed review is excluded from the DB query (its
    /// stored row is stale at this point) and re-added via <paramref name="changedApprovedRating"/>
    /// when it ends up approved.
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
            .Where(r => r.EntityId == productId && r.Status == 5 && r.Id != changedReviewId)
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
