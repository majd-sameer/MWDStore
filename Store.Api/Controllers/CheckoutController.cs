using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Localization;
using Store.Application.Orders;
using Store.Application.Shipping;
using Store.Application.ShoppingCart;
using Store.Data;
using Store.Domain;

namespace Store.Api.Controllers;

/// <summary>
/// Turns the signed-in customer's cart into an order: list shipping options for an address, then place the
/// order (snapshots the cart into a <see cref="Checkout"/> and delegates to <see cref="IOrderService"/>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/checkout")]
public sealed class CheckoutController : ControllerBase
{
    private readonly ICartService _cart;
    private readonly IOrderService _orderService;
    private readonly IShippingPriceService _shippingPriceService;
    private readonly StoreDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILocalizationService _localization;

    public CheckoutController(
        ICartService cart,
        IOrderService orderService,
        IShippingPriceService shippingPriceService,
        StoreDbContext db,
        TimeProvider timeProvider,
        ILocalizationService localization)
    {
        _cart = cart;
        _orderService = orderService;
        _shippingPriceService = shippingPriceService;
        _db = db;
        _timeProvider = timeProvider;
        _localization = localization;
    }

    /// <summary>The shipping methods (and prices) applicable to the cart for the given address.</summary>
    [HttpPost("shipping-options")]
    public async Task<ActionResult<IReadOnlyList<ShippingOptionDto>>> ShippingOptions(
        ShippingOptionsRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();
        var cart = await _cart.GetCartDetailsAsync(customerId, request.CouponCode, cancellationToken);
        if (cart == null || cart.Items.Count == 0)
        {
            return BadRequest(new { error = "Your cart is empty." });
        }

        var orderAmount = cart.Items.Sum(i => i.ProductPrice * i.Quantity);
        return await ShippingOptionsCoreAsync(orderAmount, request.ShippingAddress, cancellationToken);
    }

    /// <summary>Places the order and clears the cart. Returns the created order.</summary>
    [HttpPost("place-order")]
    public async Task<ActionResult<OrderDetailDto>> PlaceOrder(
        PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId();

        var cartItems = await _db.CartItems
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        if (cartItems.Count == 0)
        {
            return BadRequest(new { error = "Your cart is empty." });
        }

        var checkout = BuildCheckout(
            customerId, cartItems.Select(x => (x.ProductId, x.Quantity)),
            request.CouponCode, request.OrderNote, request.IsProductPriceIncludeTax);

        _db.Checkouts.Add(checkout);
        await _db.SaveChangesAsync(cancellationToken);

        var shippingAddress = request.ShippingAddress.ToOrderAddressInfo();
        var billingAddress = (request.BillingAddress ?? request.ShippingAddress).ToOrderAddressInfo();

        var result = await _orderService.CreateOrderAsync(
            checkout.Id,
            request.PaymentMethod,
            request.PaymentFeeAmount,
            request.ShippingMethodName,
            billingAddress,
            shippingAddress,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        _db.CartItems.RemoveRange(cartItems);
        await _db.SaveChangesAsync(cancellationToken);

        var order = await LoadOrderDetailAsync(result.Value!.Id, cancellationToken);
        return CreatedAtAction("GetById", "Orders", new { id = order!.Id }, order);
    }

    // Guests have no server cart, so these mirror the authed endpoints but take the cart lines in the
    // body. The order is snapshotted against the shared guest account; the shopper's real email is
    // stored on the order for the public track lookup.

    /// <summary>Shipping options for a guest's posted cart lines + address.</summary>
    [AllowAnonymous]
    [HttpPost("guest/shipping-options")]
    public async Task<ActionResult<IReadOnlyList<ShippingOptionDto>>> GuestShippingOptions(
        GuestShippingOptionsRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { error = "Your cart is empty." });
        }

        var orderAmount = await GuestOrderAmountAsync(request.Items, cancellationToken);
        return await ShippingOptionsCoreAsync(orderAmount, request.ShippingAddress, cancellationToken);
    }

    /// <summary>Places a guest order from the posted cart lines. Returns the created order (with tracking number).</summary>
    [AllowAnonymous]
    [HttpPost("guest/place-order")]
    public async Task<ActionResult<OrderDetailDto>> GuestPlaceOrder(
        GuestPlaceOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { error = "Your cart is empty." });
        }

        var guestId = await _db.Users
            .Where(u => u.Email == GuestUser.Email)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (guestId == 0)
        {
            return BadRequest(new { error = "Guest checkout is not available." });
        }

        var checkout = BuildCheckout(
            guestId, request.Items.Select(x => (x.ProductId, x.Quantity)),
            couponCode: null, request.OrderNote, request.IsProductPriceIncludeTax);

        _db.Checkouts.Add(checkout);
        await _db.SaveChangesAsync(cancellationToken);

        var shippingAddress = request.ShippingAddress.ToOrderAddressInfo();
        var billingAddress = (request.BillingAddress ?? request.ShippingAddress).ToOrderAddressInfo();

        // Email is optional. When the guest didn't supply one, synthesize a unique placeholder so every
        // order still has a distinct GuestEmail (used as the track-lookup key) rather than an empty value.
        var guestEmail = string.IsNullOrWhiteSpace(request.Email)
            ? $"guest-{Guid.NewGuid():N}@guest.local"
            : request.Email.Trim();

        var result = await _orderService.CreateOrderAsync(
            checkout.Id,
            request.PaymentMethod,
            request.PaymentFeeAmount,
            request.ShippingMethodName,
            billingAddress,
            shippingAddress,
            guestEmail: guestEmail,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        var order = await LoadOrderDetailAsync(result.Value!.Id, cancellationToken);
        return CreatedAtAction("GetById", "Orders", new { id = order!.Id }, order);
    }

    private async Task<ActionResult<IReadOnlyList<ShippingOptionDto>>> ShippingOptionsCoreAsync(
        decimal orderAmount, AddressDto shippingAddress, CancellationToken cancellationToken)
    {
        var prices = await _shippingPriceService.GetApplicableShippingPricesAsync(new GetShippingPriceRequest
        {
            OrderAmount = orderAmount,
            ShippingAddress = shippingAddress.ToOrderAddressInfo()
        }, cancellationToken);

        return Ok(prices.Select(p => new ShippingOptionDto(p.ProviderId, p.Name, p.Price)).ToList());
    }

    /// <summary>Snapshots the given lines into a new <see cref="Checkout"/> (the member and guest flows differ only in what they pass).</summary>
    private Checkout BuildCheckout(
        long customerId, IEnumerable<(long ProductId, int Quantity)> items,
        string? couponCode, string? orderNote, bool isProductPriceIncludeTax)
    {
        var now = _timeProvider.GetUtcNow();
        return new Checkout
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CreatedById = customerId,
            CreatedOn = now,
            LatestUpdatedOn = now,
            CouponCode = couponCode,
            IsProductPriceIncludeTax = isProductPriceIncludeTax,
            OrderNote = orderNote,
            CheckoutItems = items
                .Select(x => new CheckoutItem { ProductId = x.ProductId, Quantity = x.Quantity, CreatedOn = now })
                .ToList()
        };
    }

    /// <summary>Sums the (pre-discount) catalog price of the posted guest lines — the shipping-rate threshold input.</summary>
    private async Task<decimal> GuestOrderAmountAsync(
        IReadOnlyCollection<GuestCartLine> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.ProductId).ToList();
        var prices = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Price })
            .ToDictionaryAsync(p => p.Id, p => p.Price, cancellationToken);

        return items.Sum(i => prices.TryGetValue(i.ProductId, out var price) ? price * i.Quantity : 0);
    }

    private async Task<OrderDetailDto?> LoadOrderDetailAsync(long orderId, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .IncludeDetail()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            return null;
        }

        return await order.ToDetail()
            .LocalizeItemsAsync(_localization, RequestCulture.OverlayCultureId(Request), cancellationToken);
    }
}
