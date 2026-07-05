using System;

namespace Store.Domain;

/// <summary>
/// A single refund recorded against a captured <see cref="Payment"/>. A payment may have several
/// refunds (partial refunds that together may reach the captured amount). The row is the durable
/// audit trail — amount, when, who, why, and the provider's refund/transaction id — and, via
/// <see cref="IdempotencyKey"/>, the guard that makes a retried refund request a no-op.
/// </summary>
public class Refund
{
    public long Id { get; set; }

    /// <summary>The captured payment this refund draws down.</summary>
    public long PaymentId { get; set; }

    /// <summary>Denormalized order reference (a payment always belongs to one order).</summary>
    public long OrderId { get; set; }

    /// <summary>Amount refunded by this record (always positive).</summary>
    public decimal Amount { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>Id of the admin/user who requested the refund (0 for a system-issued refund).</summary>
    public long CreatedById { get; set; }

    /// <summary>Free-text reason captured from the admin (optional).</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// The provider's refund id (e.g. Stripe <c>re_…</c>). Null for manual/offline refunds
    /// (CoD and other providers settled outside a gateway).
    /// </summary>
    public string? ProviderRefundId { get; set; }

    /// <summary>
    /// Whether this refund hit an external provider (<c>true</c>, e.g. Stripe) or was recorded
    /// manually with no external call (<c>false</c>, e.g. CoD / offline).
    /// </summary>
    public bool IsManual { get; set; }

    /// <summary>
    /// Caller-supplied key that makes the refund idempotent: a second request carrying the same key
    /// for the same payment returns the existing refund instead of issuing another. Null when the
    /// caller opts out of idempotency.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public Payment Payment { get; set; } = null!;

    public Order Order { get; set; } = null!;
}
