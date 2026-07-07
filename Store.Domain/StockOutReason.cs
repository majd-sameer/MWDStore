namespace Store.Domain;

/// <summary>
/// Why a unit left the warehouse. Stored as <c>int</c>; the human labels (بيع، هدية، …) are resolved
/// on the client via i18n. <see cref="Sale"/> additionally requires a <see cref="SalesChannel"/>.
/// </summary>
public enum StockOutReason
{
    Sale = 1,          // بيع
    Gift = 2,          // هدية
    Matched = 3,       // مطابقة/تسوية
    ThirdParty = 4,    // طرف ثالث
    ExternalEvent = 5, // فعالية خارجية
    Reserved = 6,      // محجوز
    DisplayOnly = 7,   // للعرض فقط
}
