using Microsoft.EntityFrameworkCore;
using Store.Application.Catalog.Pricing;
using Store.Application.Common;
using Store.Application.Pricing.Coupons;
using Store.Data;
using Store.Domain;

namespace Store.Application.ShoppingCart;

public sealed class CartService : ICartService
{
    private readonly StoreDbContext _db;
    private readonly IProductPricingService _pricing;
    private readonly ICouponService _couponService;
    private readonly TimeProvider _timeProvider;
    private readonly IMediaUrlBuilder _mediaUrl;

    public CartService(
        StoreDbContext db, IProductPricingService pricing, ICouponService couponService,
        TimeProvider timeProvider, IMediaUrlBuilder mediaUrl)
    {
        _db = db;
        _pricing = pricing;
        _couponService = couponService;
        _timeProvider = timeProvider;
        _mediaUrl = mediaUrl;
    }

    /// <summary>
    /// Adds a product to the bag, folding into the existing line when there is one.
    ///
    /// <para>
    /// Stock is enforced here, not just reported at checkout. <c>Product.StockQuantity</c> is already
    /// net of every order that holds units — an order takes its stock the moment it is placed and only
    /// returns it if it is canceled (payment timeout, decline, admin) — so a bag can never be filled
    /// with units another shopper's unpaid order is still holding.
    /// </para>
    /// </summary>
    public async Task<CartLineResult> AddToCartAsync(
        long customerId, long productId, int quantity, CancellationToken cancellationToken = default)
    {
        var result = new CartLineResult
        {
            Success = false,
            ProductId = productId,
            RequestedQuantity = quantity
        };

        if (quantity <= 0)
        {
            return Fail(result, "wrong-quantity", "The quantity must be larger than zero");
        }

        var product = await _db.Set<Product>()
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);
        if (product == null)
        {
            // Previously this fell through to a foreign-key violation at SaveChanges.
            return Fail(result, "product-not-found", "That product no longer exists");
        }

        if (!product.IsPublished || !product.IsAllowToOrder)
        {
            return Fail(result, "unavailable", $"{product.Name} is not available to order");
        }

        var cartItem = await _db.Set<CartItem>()
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId, cancellationToken);

        var current = cartItem?.Quantity ?? 0;
        int target;

        if (product.StockTrackingIsEnabled)
        {
            var available = Math.Max(0, product.StockQuantity);
            result.AvailableQuantity = available;

            if (current >= available)
            {
                // Nothing can be added — either the product is sold out, or the bag already holds
                // every unit there is (which is also what a shopper sees when someone else's pending
                // order is holding them).
                return Fail(result, "out-of-stock", available == 0
                    ? $"{product.Name} is out of stock"
                    : $"There are only {available} of {product.Name} available, and they are already in your bag");
            }

            target = Math.Min(current + quantity, available);
            result.WasCapped = target < current + quantity;
        }
        else
        {
            if ((long)current + quantity > int.MaxValue)
            {
                return Fail(result, "wrong-quantity", "The quantity must be larger than zero");
            }

            target = current + quantity;
        }

        var now = _timeProvider.GetUtcNow();
        if (cartItem == null)
        {
            _db.Set<CartItem>().Add(new CartItem
            {
                CustomerId = customerId,
                ProductId = productId,
                Quantity = target,
                CreatedOn = now,
                LatestUpdatedOn = now
            });
        }
        else
        {
            cartItem.Quantity = target;
            cartItem.LatestUpdatedOn = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        result.Success = true;
        result.Quantity = target;
        return result;
    }

    /// <summary>
    /// Sets a line's quantity outright. Like the add path this is capped by stock rather than
    /// refused, so a stepper pushed past what is left settles on the maximum instead of failing.
    /// </summary>
    public async Task<CartLineResult> UpdateQuantityAsync(
        long customerId, long cartItemId, int quantity, CancellationToken cancellationToken = default)
    {
        var result = new CartLineResult { Success = false, RequestedQuantity = quantity };

        if (quantity <= 0)
        {
            return Fail(result, "wrong-quantity", "The quantity must be larger than zero");
        }

        var cartItem = await _db.Set<CartItem>()
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == cartItemId && x.CustomerId == customerId, cancellationToken);
        if (cartItem == null)
        {
            return Fail(result, "not-found", "Cart item not found");
        }

        result.ProductId = cartItem.ProductId;
        var product = cartItem.Product;
        var target = quantity;

        if (product.StockTrackingIsEnabled)
        {
            var available = Math.Max(0, product.StockQuantity);
            result.AvailableQuantity = available;

            if (available == 0)
            {
                return Fail(result, "out-of-stock", $"{product.Name} is out of stock");
            }

            target = Math.Min(quantity, available);
            result.WasCapped = target < quantity;
        }

        cartItem.Quantity = target;
        cartItem.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        result.Success = true;
        result.Quantity = target;
        return result;
    }

    private static CartLineResult Fail(CartLineResult result, string code, string message)
    {
        result.Success = false;
        result.ErrorCode = code;
        result.ErrorMessage = message;
        return result;
    }

    public async Task<bool> RemoveItemAsync(
        long customerId, long cartItemId, CancellationToken cancellationToken = default)
    {
        var cartItem = await _db.Set<CartItem>()
            .FirstOrDefaultAsync(x => x.Id == cartItemId && x.CustomerId == customerId, cancellationToken);
        if (cartItem == null)
        {
            return false;
        }

        _db.Set<CartItem>().Remove(cartItem);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CartModel?> GetCartDetailsAsync(
        long customerId, string? couponCode = null, CancellationToken cancellationToken = default)
    {
        var cartItems = await _db.Set<CartItem>()
            .AsNoTracking()
            .Include(x => x.Product).ThenInclude(p => p.ThumbnailImage)
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            return null;
        }

        var cart = new CartModel { CustomerId = customerId, CouponCode = couponCode };

        cart.Items = cartItems
            .Select(x => new CartItemModel
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                ProductImageUrl = _mediaUrl.GetUrl(x.Product.ThumbnailImage?.FileName),
                ProductPrice = x.Product.Price,
                CalculatedProductPrice = _pricing.CalculateProductPrice(x.Product),
                Quantity = x.Quantity,
                ProductStockQuantity = x.Product.StockQuantity,
                ProductStockTrackingIsEnabled = x.Product.StockTrackingIsEnabled,
                IsProductAvailableToOrder = x.Product.IsAllowToOrder && x.Product.IsPublished && !x.Product.IsDeleted,
                IsAvailable = x.Product.IsAllowToOrder && x.Product.IsPublished && !x.Product.IsDeleted
                    && (!x.Product.StockTrackingIsEnabled || x.Product.StockQuantity >= x.Quantity),
                AvailableQuantity = x.Product.StockTrackingIsEnabled
                    ? Math.Max(0, x.Product.StockQuantity)
                    : x.Quantity
            })
            .ToList();

        // Only buyable lines are priced. An out-of-stock or withdrawn line stays in the cart so the
        // shopper can see what happened to it — typically because their failed order was returned to
        // the cart — but charging for it, or discounting it, would be a lie.
        var buyable = cart.Items.Where(x => x.IsAvailable).ToList();

        cart.SubTotal = buyable.Sum(x => x.Quantity * (x.CalculatedProductPrice.OldPrice ?? x.ProductPrice));

        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var cartInfo = new CartInfoForCoupon
            {
                Items = buyable.Select(x => new CartItemForCoupon { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
            };

            var couponResult = await _couponService.ValidateAsync(customerId, cart.CouponCode, cartInfo, cancellationToken);
            if (couponResult.Succeeded)
            {
                cart.Discount = couponResult.DiscountAmount;
            }
            else
            {
                cart.CouponValidationErrorMessage = couponResult.ErrorMessage;
            }
        }

        cart.Discount += buyable
            .Where(x => x.CalculatedProductPrice.OldPrice.HasValue)
            .Sum(x => x.Quantity * (x.CalculatedProductPrice.OldPrice!.Value - x.CalculatedProductPrice.Price));

        return cart;
    }
}
