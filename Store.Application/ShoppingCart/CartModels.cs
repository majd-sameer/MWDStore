using Store.Application.Catalog.Pricing;

namespace Store.Application.ShoppingCart;

/// <summary>Outcome of an add-to-cart attempt.</summary>
public sealed class AddToCartResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ErrorCode { get; set; }
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

    public decimal SubTotal { get; set; }

    public decimal Discount { get; set; }
}
