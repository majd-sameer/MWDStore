namespace Store.Application.ShoppingCart;

/// <summary>
/// The cart is simply the set of <c>CartItem</c> rows for a customer — there is no cart header
/// entity; totals are computed on the fly.
/// </summary>
public interface ICartService
{
    /// <summary>
    /// Adds a product to the cart, folding into the existing line when there is one. Stock caps the
    /// result — see <see cref="CartLineResult"/>.
    /// </summary>
    Task<CartLineResult> AddToCartAsync(
        long customerId, long productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Sets the quantity of an existing cart line (must be &gt; 0), capped by stock.</summary>
    Task<CartLineResult> UpdateQuantityAsync(
        long customerId, long cartItemId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Removes a cart line owned by the customer. Returns false if not found.</summary>
    Task<bool> RemoveItemAsync(
        long customerId, long cartItemId, CancellationToken cancellationToken = default);

    /// <summary>Computes the cart totals, optionally applying a coupon code.</summary>
    Task<CartModel?> GetCartDetailsAsync(
        long customerId, string? couponCode = null, CancellationToken cancellationToken = default);
}
