using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.ShoppingCart;

namespace Store.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public sealed class CartController : ControllerBase
{
    private readonly ICartService _cart;

    public CartController(ICartService cart) => _cart = cart;

    [HttpGet]
    public async Task<ActionResult<CartModel>> Get(
        [FromQuery] string? couponCode, CancellationToken cancellationToken)
    {
        var cart = await _cart.GetCartDetailsAsync(User.GetUserId(), couponCode, cancellationToken);
        // An empty cart is a valid state — return an empty cart model rather than 404.
        return Ok(cart ?? new CartModel { CustomerId = User.GetUserId(), CouponCode = couponCode });
    }

    /// <summary>
    /// Adds a product, merging into the existing line when present. Stock caps the line rather than
    /// rejecting the request: a partial add succeeds and reports the cap on
    /// <see cref="CartModel.Adjustment"/>. Only an add that could take nothing is a 400.
    /// </summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartModel>> AddItem(AddToCartRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var result = await _cart.AddToCartAsync(customerId, request.ProductId, request.Quantity, cancellationToken);
        return await RespondAsync(customerId, result, cancellationToken);
    }

    /// <summary>Sets the quantity of an existing cart line (capped by stock, like the add).</summary>
    [HttpPut("items/{cartItemId:long}")]
    public async Task<ActionResult<CartModel>> UpdateItem(
        long cartItemId, UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var result = await _cart.UpdateQuantityAsync(customerId, cartItemId, request.Quantity, cancellationToken);
        return await RespondAsync(customerId, result, cancellationToken);
    }

    /// <summary>
    /// Turns a cart write into its response: the refreshed cart, carrying the stock adjustment when
    /// the write was capped. A line the shopper no longer owns is a 404; everything else is a 400.
    /// </summary>
    private async Task<ActionResult<CartModel>> RespondAsync(
        long customerId, CartLineResult result, CancellationToken cancellationToken)
    {
        if (!result.Success)
        {
            var body = new { error = result.ErrorMessage, code = result.ErrorCode, available = result.AvailableQuantity };
            return result.ErrorCode == "not-found" ? NotFound(body) : BadRequest(body);
        }

        var cart = await _cart.GetCartDetailsAsync(customerId, null, cancellationToken)
            ?? new CartModel { CustomerId = customerId };

        if (result.WasCapped)
        {
            cart.Adjustment = new CartLineAdjustment(
                result.ProductId, result.RequestedQuantity, result.Quantity, result.AvailableQuantity ?? result.Quantity);
        }

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
