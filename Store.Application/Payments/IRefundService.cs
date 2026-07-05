using Store.Application.Common;

namespace Store.Application.Payments;

/// <summary>
/// Refunds a captured order payment — in full or in part — against the original provider. Validates the
/// request (order must be paid; the requested amount cannot exceed the captured amount minus what has
/// already been refunded), executes the refund (Stripe via the gateway; CoD/manual providers just record
/// the refund with no external call), persists a <c>Refund</c> audit row, and advances the payment status
/// to <c>PartiallyRefunded</c> or <c>Refunded</c> (fully refunding also moves the order to
/// <c>OrderStatus.Refunded</c>). Idempotent when the caller supplies an <c>IdempotencyKey</c>: a retry with
/// the same key returns the original refund instead of issuing a second one.
/// </summary>
public interface IRefundService
{
    Task<Result<RefundResult>> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A request to refund an order.</summary>
/// <param name="OrderId">The order whose captured payment should be refunded.</param>
/// <param name="Amount">The amount to refund; <c>null</c> refunds the full remaining captured amount.</param>
/// <param name="Reason">Optional admin-supplied reason, recorded on the refund and passed to the provider.</param>
/// <param name="RequestedByUserId">Id of the admin/user issuing the refund (0 for a system action).</param>
/// <param name="IdempotencyKey">
/// Optional caller-stable key. When supplied, a second request with the same key for the same payment is a
/// no-op that returns the original refund (see <see cref="RefundResult.AlreadyProcessed"/>).
/// </param>
public sealed record RefundRequest(
    long OrderId,
    decimal? Amount,
    string? Reason,
    long RequestedByUserId,
    string? IdempotencyKey);

/// <summary>Outcome of a refund.</summary>
/// <param name="RefundId">The persisted <c>Refund</c> row id.</param>
/// <param name="OrderId">The refunded order.</param>
/// <param name="PaymentId">The captured payment the refund drew down.</param>
/// <param name="Amount">The amount this refund returned.</param>
/// <param name="TotalRefunded">Total refunded against the payment after this refund.</param>
/// <param name="PaymentStatus">The payment's status after this refund (<c>PartiallyRefunded</c>/<c>Refunded</c>).</param>
/// <param name="FullyRefunded">True when the captured amount is now entirely refunded.</param>
/// <param name="ProviderRefundId">The provider's refund id (null for manual/offline refunds).</param>
/// <param name="AlreadyProcessed">
/// True when this call matched an existing refund by idempotency key and no new refund was issued.
/// </param>
public sealed record RefundResult(
    long RefundId,
    long OrderId,
    long PaymentId,
    decimal Amount,
    decimal TotalRefunded,
    int PaymentStatus,
    bool FullyRefunded,
    string? ProviderRefundId,
    bool AlreadyProcessed);
