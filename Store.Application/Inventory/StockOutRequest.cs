using Store.Domain;

namespace Store.Application.Inventory;

/// <summary>
/// A request to take <see cref="Quantity"/> units of a product out of one warehouse for a business
/// <see cref="Reason"/>. <see cref="Channel"/> is required when the reason is
/// <see cref="StockOutReason.Sale"/>. <see cref="PerformedById"/> is who physically removed the
/// stock; it defaults to the recording user and may only be overridden by admins.
/// </summary>
public sealed class StockOutRequest
{
    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    /// <summary>Units removed — a positive number.</summary>
    public int Quantity { get; set; }

    public StockOutReason Reason { get; set; }

    public SalesChannel? Channel { get; set; }

    public long? PerformedById { get; set; }

    public string? RecipientOrRef { get; set; }

    public string? Note { get; set; }
}
