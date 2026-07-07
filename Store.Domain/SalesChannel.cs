namespace Store.Domain;

/// <summary>
/// The channel a stock-out sale went through. Stored as <c>int</c>; required when the
/// <see cref="StockOutReason"/> is <see cref="StockOutReason.Sale"/>, optional otherwise.
/// </summary>
public enum SalesChannel
{
    Showroom = 1,           // صالة العرض
    ExternalExhibition = 2, // معرض خارجي
    ExternalBroker = 3,     // وسيط خارجي
    LocalBroker = 4,        // وسيط محلي
    OnlineStore = 5,        // المتجر الإلكتروني
}
