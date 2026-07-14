using Store.Application.Orders;

namespace Store.Api.Infrastructure;

/// <summary>Maps integer <see cref="OrderStatus"/> codes to display names.</summary>
public static class OrderStatusNames
{
    public static string For(int status) => status switch
    {
        OrderStatus.New => "New",
        OrderStatus.OnHold => "OnHold",
        OrderStatus.PendingPayment => "PendingPayment",
        OrderStatus.PaymentReceived => "PaymentReceived",
        OrderStatus.PaymentFailed => "PaymentFailed",
        OrderStatus.Invoiced => "Invoiced",
        OrderStatus.Shipping => "Shipping",
        OrderStatus.Shipped => "Shipped",
        OrderStatus.Complete => "Complete",
        OrderStatus.Canceled => "Canceled",
        OrderStatus.Refunded => "Refunded",
        OrderStatus.Closed => "Closed",
        _ => status.ToString()
    };
}
