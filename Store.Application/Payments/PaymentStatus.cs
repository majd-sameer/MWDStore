namespace Store.Application.Payments;

/// <summary>
/// Port of SimplCommerce's <c>PaymentStatus</c> values (the domain stores the status as an <c>int</c>
/// on <see cref="Store.Domain.Payment.Status"/>).
/// </summary>
public static class PaymentStatus
{
    /// <summary>Payment row created, redirect issued, awaiting the gateway result.</summary>
    public const int PendingExecution = -10;

    public const int Pending = 0;

    public const int Failed = 10;

    /// <summary>Authorized (funds held) but not yet captured. Not used by the current sandbox flow,
    /// included so the model covers the standard authorize/capture lifecycle.</summary>
    public const int Authorized = 15;

    /// <summary>Captured / paid — funds settled. (Named <c>Succeeded</c> for SimplCommerce parity;
    /// this is the "Paid" state a refund draws down from.)</summary>
    public const int Succeeded = 20;

    /// <summary>At least one refund issued, but the captured amount is not yet fully refunded.</summary>
    public const int PartiallyRefunded = 25;

    /// <summary>Fully refunded — the captured amount has been returned in one or more refunds.</summary>
    public const int Refunded = 30;

    public const int Voided = 40;
}
