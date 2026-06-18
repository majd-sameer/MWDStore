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

    public const int Succeeded = 20;

    public const int Refunded = 30;

    public const int Voided = 40;
}
