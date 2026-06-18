namespace Store.Application.Orders;

/// <summary>
/// SimplCommerce's <c>OrderStatus</c> values (the domain stores the status as an <c>int</c>).
/// </summary>
public static class OrderStatus
{
    public const int New = 1;
    public const int OnHold = 10;
    public const int PendingPayment = 20;
    public const int PaymentReceived = 30;
    public const int PaymentFailed = 35;
    public const int Invoiced = 40;
    public const int Shipping = 50;
    public const int Shipped = 60;
    public const int Complete = 70;
    public const int Canceled = 80;
    public const int Refunded = 90;
    public const int Closed = 100;
}
