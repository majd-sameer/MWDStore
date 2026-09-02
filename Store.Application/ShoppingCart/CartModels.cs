using Store.Application.Catalog.Pricing;

namespace Store.Application.ShoppingCart;

/// <summary>
/// Outcome of a cart write (add or set-quantity).
///
/// Stock is a hard ceiling on what a bag may hold: <see cref="AvailableQuantity"/> is the product's
/// stock on hand, which is already net of every order that has taken units (an order decrements at
/// placement and only gives them back if it is canceled). A request for more than that is not
/// refused outright — it is <b>capped</b>, <see cref="WasCapped"/> is set and the caller tells the
/// shopper. Only a request that could add nothing at all fails, with <c>out-of-stock</c>.
/// </summary>
public sealed class CartLineResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary><c>wrong-quantity</c>, <c>product-not-found</c>, <c>unavailable</c>, <c>out-of-stock</c> or <c>not-found</c>.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>The product the write targeted (0 when it could not be resolved).</summary>
    public long ProductId { get; set; }

    /// <summary>What the shopper asked for — the delta on an add, the absolute value on an update.</summary>
    public int RequestedQuantity { get; set; }

    /// <summary>The line's quantity after the write.</summary>
    public int Quantity { get; set; }

    /// <summary>The stock ceiling that applied, or null when the product is not stock-tracked.</summary>
    public int? AvailableQuantity { get; set; }

    /// <summary>True when stock cut the request short and the line holds less than was asked for.</summary>
    public bool WasCapped { get; set; }
}

/// <summary>A single cart line with the product's resolved (catalog) price.</summary>
public sealed class CartItemModel
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ProductImageUrl { get; set; }

    public decimal ProductPrice { get; set; }

    public CalculatedProductPrice CalculatedProductPrice { get; set; } = new();

    public int Quantity { get; set; }

    public long ProductStockQuantity { get; set; }

    public bool ProductStockTrackingIsEnabled { get; set; }

    public bool IsProductAvailableToOrder { get; set; }

    /// <summary>
    /// False when this line cannot be bought as it stands — the product was withdrawn, or stock
    /// tracking says fewer are left than the line asks for. Such lines stay in the cart for the
    /// shopper to see but are **excluded from <see cref="CartModel.SubTotal"/> and
    /// <see cref="CartModel.Discount"/>**, so the totals only ever cover what is actually buyable.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// How many of this product can be bought right now: the stock on hand for a stock-tracked
    /// product, otherwise the line's own quantity (nothing constrains it).
    /// </summary>
    public long AvailableQuantity { get; set; }
}

/// <summary>
/// The computed cart. <see cref="SubTotal"/> is summed at the regular
/// (pre-discount) price; <see cref="Discount"/> folds the coupon discount together with catalog
/// special/old-price savings. Tax and shipping are not part of the cart — they are computed at checkout.
/// </summary>
public sealed class CartModel
{
    public long CustomerId { get; set; }

    public string? CouponCode { get; set; }

    public string? CouponValidationErrorMessage { get; set; }

    public List<CartItemModel> Items { get; set; } = [];

    /// <summary>
    /// Set only on the cart returned from an add/update that stock cut short, so the storefront can
    /// say "only N left, we put N in your bag". Always null on a plain read.
    /// </summary>
    public CartLineAdjustment? Adjustment { get; set; }

    public decimal SubTotal { get; set; }

    public decimal Discount { get; set; }
}

/// <summary>
/// Reports that a cart write was capped by stock: the shopper asked for
/// <paramref name="RequestedQuantity"/> but the line holds <paramref name="AppliedQuantity"/>,
/// which is all the stock allows.
/// </summary>
public sealed record CartLineAdjustment(
    long ProductId, int RequestedQuantity, int AppliedQuantity, int AvailableQuantity);
