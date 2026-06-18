using Microsoft.EntityFrameworkCore;
using Store.Application.Catalog.Pricing;
using Store.Application.Common;
using Store.Application.Pricing.Coupons;
using Store.Data;
using Store.Domain;

namespace Store.Application.ShoppingCart;

/// <summary>
/// Faithful port of SimplCommerce's <c>CartService</c> (<c>Module.ShoppingCart/Services/CartService.cs</c>):
/// add-to-cart merging, and the display-time total computation (regular sub-total, coupon + catalog savings
/// folded into a single discount). Media URLs, variation-option display and currency formatting are out of scope.
/// Update/remove are provided here (SimplCommerce keeps them in the controller layer) for a complete cart API.
/// </summary>
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

    public async Task<AddToCartResult> AddToCartAsync(
        long customerId, long productId, int quantity, CancellationToken cancellationToken = default)
    {
        var result = new AddToCartResult { Success = false };

        if (quantity <= 0)
        {
            result.ErrorMessage = "The quantity must be larger than zero";
            result.ErrorCode = "wrong-quantity";
            return result;
        }

        var cartItem = await _db.Set<CartItem>()
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId, cancellationToken);

        if (cartItem == null)
        {
            cartItem = new CartItem
            {
                CustomerId = customerId,
                ProductId = productId,
                Quantity = quantity,
                CreatedOn = _timeProvider.GetUtcNow(),
                LatestUpdatedOn = _timeProvider.GetUtcNow()
            };

            _db.Set<CartItem>().Add(cartItem);
        }
        else
        {
            if ((long)cartItem.Quantity + quantity > int.MaxValue)
            {
                result.ErrorMessage = "The quantity must be larger than zero";
                result.ErrorCode = "wrong-quantity";
                return result;
            }

            cartItem.Quantity += quantity;
            cartItem.LatestUpdatedOn = _timeProvider.GetUtcNow();
        }

        await _db.SaveChangesAsync(cancellationToken);

        result.Success = true;
        return result;
    }

    public async Task<bool> UpdateQuantityAsync(
        long customerId, long cartItemId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return false;
        }

        var cartItem = await _db.Set<CartItem>()
            .FirstOrDefaultAsync(x => x.Id == cartItemId && x.CustomerId == customerId, cancellationToken);
        if (cartItem == null)
        {
            return false;
        }

        cartItem.Quantity = quantity;
        cartItem.LatestUpdatedOn = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
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
                IsProductAvailableToOrder = x.Product.IsAllowToOrder && x.Product.IsPublished && !x.Product.IsDeleted
            })
            .ToList();

        // SubTotal is summed at the regular (pre-discount) price: OldPrice when a catalog discount exists,
        // otherwise the plain product price.
        cart.SubTotal = cart.Items.Sum(x => x.Quantity * (x.CalculatedProductPrice.OldPrice ?? x.ProductPrice));

        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var cartInfo = new CartInfoForCoupon
            {
                Items = cart.Items.Select(x => new CartItemForCoupon { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
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

        // Catalog (special/old-price) savings are folded into the same Discount field as the coupon.
        cart.Discount += cart.Items
            .Where(x => x.CalculatedProductPrice.OldPrice.HasValue)
            .Sum(x => x.Quantity * (x.CalculatedProductPrice.OldPrice!.Value - x.CalculatedProductPrice.Price));

        return cart;
    }
}
