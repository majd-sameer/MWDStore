using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.ShoppingCart;

namespace Store.Api.Controllers;

/// <summary>The signed-in customer's shopping cart.</summary>
[ApiController]
[Authorize]
[Route("api/cart")]
public sealed class CartController : ControllerBase
{
    private readonly ICartService _cart;

    public CartController(ICartService cart) => _cart = cart;

    /// <summary>The current cart with computed sub-total and discount; optionally validates a coupon.</summary>
    [HttpGet]
    public async Task<ActionResult<CartModel>> Get(
        [FromQuery] string? couponCode, CancellationToken cancellationToken)
    {
        var cart = await _cart.GetCartDetailsAsync(User.GetUserId(), couponCode, cancellationToken);
        // An empty cart is a valid state — return an empty cart model rather than 404.
        return Ok(cart ?? new CartModel { CustomerId = User.GetUserId(), CouponCode = couponCode });
    }

    /// <summary>Adds a product (merges into the existing line when present).</summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartModel>> AddItem(AddToCartRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var result = await _cart.AddToCartAsync(customerId, request.ProductId, request.Quantity, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage, code = result.ErrorCode });
        }

        var cart = await _cart.GetCartDetailsAsync(customerId, null, cancellationToken);
        return Ok(cart);
    }

    /// <summary>Sets the quantity of an existing cart line.</summary>
    [HttpPut("items/{cartItemId:long}")]
    public async Task<ActionResult<CartModel>> UpdateItem(
        long cartItemId, UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var updated = await _cart.UpdateQuantityAsync(customerId, cartItemId, request.Quantity, cancellationToken);
        if (!updated)
        {
            return NotFound(new { error = "Cart item not found or quantity invalid." });
        }

        var cart = await _cart.GetCartDetailsAsync(customerId, null, cancellationToken);
        return Ok(cart);
    }

    /// <summary>Removes a cart line.</summary>
    [HttpDelete("items/{cartItemId:long}")]
    public async Task<IActionResult> RemoveItem(long cartItemId, CancellationToken cancellationToken)
    {
        var removed = await _cart.RemoveItemAsync(User.GetUserId(), cartItemId, cancellationToken);
        return removed ? NoContent() : NotFound(new { error = "Cart item not found." });
    }
}
