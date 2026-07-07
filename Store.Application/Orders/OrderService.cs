using Microsoft.EntityFrameworkCore;
using Store.Application.Catalog.Pricing;
using Store.Application.Common;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.Tax;
using Store.Data;
using Store.Domain;

namespace Store.Application.Orders;

/// <summary>
/// Faithful port of SimplCommerce's <c>OrderService.CreateOrder</c> core overload
/// (<c>Module.Orders/Services/OrderService.cs</c>): per-line tax/price/discount resolution, stock decrement,
/// and the order-level rollup performed in SimplCommerce's exact order
/// (discount → shipping → tax → subtotal → subtotal-with-discount → grand total).
/// </summary>
/// <remarks>
/// Deviations from the original, all behavior-preserving: the address-resolving overload (JSON
/// <c>ShippingData</c> deserialization + <c>UserAddress</c> creation) is replaced by an
/// <see cref="OrderAddressInfo"/> input; the order's <c>Customer</c>/<c>CreatedBy</c> navigations are set by
/// id; explicit DB transactions and MediatR events are omitted (a single <c>SaveChanges</c> persists stock,
/// order(s) and coupon usage). The marketplace sub-order split is preserved.
/// </remarks>
public sealed class OrderService : IOrderService
{
    private readonly StoreDbContext _db;
    private readonly ICouponService _couponService;
    private readonly ITaxService _taxService;
    private readonly IShippingPriceService _shippingPriceService;
    private readonly IProductPricingService _productPricingService;
    private readonly TimeProvider _timeProvider;

    public OrderService(
        StoreDbContext db,
        ICouponService couponService,
        ITaxService taxService,
        IShippingPriceService shippingPriceService,
        IProductPricingService productPricingService,
        TimeProvider timeProvider)
    {
        _db = db;
        _couponService = couponService;
        _taxService = taxService;
        _shippingPriceService = shippingPriceService;
        _productPricingService = productPricingService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Order>> CreateOrderAsync(
        Guid checkoutId,
        string? paymentMethod,
        decimal paymentFeeAmount,
        string shippingMethodName,
        OrderAddressInfo billingAddress,
        OrderAddressInfo shippingAddress,
        int orderStatus = OrderStatus.New,
        string? guestEmail = null,
        CancellationToken cancellationToken = default)
    {
        var checkout = await _db.Set<Checkout>()
            .Include(c => c.CheckoutItems).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == checkoutId, cancellationToken);

        if (checkout == null)
        {
            return Result.Fail<Order>($"Checkout id {checkoutId} cannot be found");
        }

        var couponResult = await CheckForDiscountIfAnyAsync(checkout, cancellationToken);
        if (!couponResult.Succeeded)
        {
            return Result.Fail<Order>(couponResult.ErrorMessage ?? "Coupon is not valid");
        }

        var shippingMethodResult = await ValidateShippingMethodAsync(shippingMethodName, shippingAddress, checkout, cancellationToken);
        if (!shippingMethodResult.Success)
        {
            return Result.Fail<Order>(shippingMethodResult.Error!);
        }

        var shippingMethod = shippingMethodResult.Value!;
        var now = _timeProvider.GetUtcNow();

        var orderBillingAddress = ToOrderAddress(billingAddress);
        var orderShippingAddress = ToOrderAddress(shippingAddress);

        var order = new Order
        {
            CustomerId = checkout.CustomerId,
            CreatedOn = now,
            CreatedById = checkout.CreatedById,
            LatestUpdatedOn = now,
            LatestUpdatedById = checkout.CreatedById,
            BillingAddress = orderBillingAddress,
            ShippingAddress = orderShippingAddress,
            PaymentMethod = paymentMethod,
            PaymentFeeAmount = paymentFeeAmount,
            GuestEmail = string.IsNullOrWhiteSpace(guestEmail) ? null : guestEmail.Trim(),
            TrackingNumber = await GenerateUniqueTrackingNumberAsync(cancellationToken)
        };

        foreach (var checkoutItem in checkout.CheckoutItems)
        {
            var product = checkoutItem.Product;

            if (!product.IsAllowToOrder || !product.IsPublished || product.IsDeleted)
            {
                return Result.Fail<Order>($"The product {product.Name} is not available any more");
            }

            if (product.StockTrackingIsEnabled && product.StockQuantity < checkoutItem.Quantity)
            {
                return Result.Fail<Order>($"There are only {product.StockQuantity} items available for {product.Name}");
            }

            var taxPercent = await _taxService.GetTaxPercentAsync(
                product.TaxClassId, shippingAddress.CountryId, shippingAddress.StateOrProvinceId, shippingAddress.ZipCode, cancellationToken);

            var calculatedProductPrice = _productPricingService.CalculateProductPrice(product);

            // Use the regular (pre-discount) price as the line base, then strip tax out if prices include it.
            var productPrice = calculatedProductPrice.OldPrice ?? calculatedProductPrice.Price;
            if (checkout.IsProductPriceIncludeTax)
            {
                productPrice /= 1 + (taxPercent / 100);
            }

            var orderItem = new OrderItem
            {
                Product = product,
                ProductId = product.Id,
                ProductPrice = productPrice,
                Quantity = checkoutItem.Quantity,
                TaxPercent = taxPercent,
                TaxAmount = checkoutItem.Quantity * (productPrice * taxPercent / 100)
            };

            var discountedItem = couponResult.DiscountedProducts.FirstOrDefault(x => x.Id == checkoutItem.ProductId);
            if (discountedItem != null)
            {
                orderItem.DiscountAmount = discountedItem.DiscountAmount;
            }

            // Fold the catalog special/old-price saving into the line discount.
            if (calculatedProductPrice.OldPrice.HasValue)
            {
                orderItem.DiscountAmount += orderItem.Quantity * (calculatedProductPrice.OldPrice.Value - calculatedProductPrice.Price);
            }

            order.OrderItems.Add(orderItem);

            if (product.StockTrackingIsEnabled)
            {
                product.StockQuantity -= checkoutItem.Quantity;
                await StampOnlineSaleAsync(product.Id, checkoutItem.Quantity, checkout.CreatedById, now, cancellationToken);
            }
        }

        // Order-level rollup — SimplCommerce's exact ordering of totals.
        order.OrderStatus = orderStatus;
        order.OrderNote = checkout.OrderNote;
        order.CouponCode = couponResult.CouponCode;
        order.CouponRuleName = checkout.CouponRuleName;
        order.DiscountAmount = couponResult.DiscountAmount + order.OrderItems.Sum(x => x.DiscountAmount);
        order.ShippingFeeAmount = shippingMethod.Price;
        order.ShippingMethod = shippingMethod.Name;
        order.TaxAmount = order.OrderItems.Sum(x => x.TaxAmount);
        order.SubTotal = order.OrderItems.Sum(x => x.ProductPrice * x.Quantity);
        order.SubTotalWithDiscount = order.SubTotal - couponResult.DiscountAmount;
        order.OrderTotal = order.SubTotal + order.TaxAmount + order.ShippingFeeAmount + order.PaymentFeeAmount - order.DiscountAmount;

        _db.Set<Order>().Add(order);

        var vendorIds = checkout.CheckoutItems
            .Where(x => x.Product.VendorId.HasValue)
            .Select(x => x.Product.VendorId!.Value)
            .Distinct()
            .ToList();

        if (vendorIds.Count > 0)
        {
            order.IsMasterOrder = true;
        }

        foreach (var vendorId in vendorIds)
        {
            var subOrder = new Order
            {
                CustomerId = checkout.CustomerId,
                CreatedOn = now,
                CreatedById = checkout.CreatedById,
                LatestUpdatedOn = now,
                LatestUpdatedById = checkout.CreatedById,
                BillingAddress = orderBillingAddress,
                ShippingAddress = orderShippingAddress,
                VendorId = vendorId,
                Parent = order,
                OrderStatus = orderStatus
            };

            foreach (var cartItem in checkout.CheckoutItems.Where(x => x.Product.VendorId == vendorId))
            {
                var taxPercent = await _taxService.GetTaxPercentAsync(
                    cartItem.Product.TaxClassId, shippingAddress.CountryId, shippingAddress.StateOrProvinceId, shippingAddress.ZipCode, cancellationToken);

                // Sub-orders use the raw product price (not the calculated price).
                var productPrice = cartItem.Product.Price;
                if (checkout.IsProductPriceIncludeTax)
                {
                    productPrice /= 1 + (taxPercent / 100);
                }

                var orderItem = new OrderItem
                {
                    Product = cartItem.Product,
                    ProductId = cartItem.ProductId,
                    ProductPrice = productPrice,
                    Quantity = cartItem.Quantity,
                    TaxPercent = taxPercent,
                    TaxAmount = cartItem.Quantity * (productPrice * taxPercent / 100)
                };

                if (checkout.IsProductPriceIncludeTax)
                {
                    orderItem.ProductPrice -= orderItem.TaxAmount;
                }

                subOrder.OrderItems.Add(orderItem);
            }

            subOrder.SubTotal = subOrder.OrderItems.Sum(x => x.ProductPrice * x.Quantity);
            subOrder.TaxAmount = subOrder.OrderItems.Sum(x => x.TaxAmount);
            subOrder.OrderTotal = subOrder.SubTotal + subOrder.TaxAmount + subOrder.ShippingFeeAmount - subOrder.DiscountAmount;
            _db.Set<Order>().Add(subOrder);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Record coupon usage against the persisted order id, then commit.
        _couponService.AddCouponUsage(checkout.CustomerId, order.Id, couponResult);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(order);
    }

    /// <summary>
    /// Records an online-store sale against the product's primary warehouse stock so the movement
    /// shows in the stock-out log stamped Sale / OnlineStore (performer null — no staff removed it).
    /// No-op when the product has no per-warehouse <c>Stock</c> row. Does not touch
    /// <c>Product.StockQuantity</c> (the caller already did) and does not save — the order's single
    /// <c>SaveChanges</c> persists it atomically.
    /// </summary>
    private async Task StampOnlineSaleAsync(
        long productId, int quantity, long createdById, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stock = await _db.Set<Stock>()
            .Where(s => s.ProductId == productId)
            .OrderByDescending(s => s.Quantity)
            .FirstOrDefaultAsync(cancellationToken);
        if (stock is null)
        {
            return;
        }

        stock.Quantity -= quantity;
        if (stock.Quantity < 0)
        {
            stock.Quantity = 0;
        }

        _db.Set<StockHistory>().Add(new StockHistory
        {
            ProductId = productId,
            WarehouseId = stock.WarehouseId,
            AdjustedQuantity = -quantity,
            Reason = StockOutReason.Sale,
            Channel = SalesChannel.OnlineStore,
            PerformedById = null,
            CreatedById = createdById,
            CreatedOn = now,
        });
    }

    public async Task CancelOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.OrderStatus = OrderStatus.Canceled;
        order.LatestUpdatedOn = _timeProvider.GetUtcNow();

        var orderItems = await _db.Set<OrderItem>()
            .Include(x => x.Product)
            .Where(x => x.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in orderItems)
        {
            if (item.Product.StockTrackingIsEnabled)
            {
                item.Product.StockQuantity += item.Quantity;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> GetTaxAsync(
        Guid checkoutId, string? countryId, long stateOrProvinceId, string? zipCode,
        CancellationToken cancellationToken = default)
    {
        decimal taxAmount = 0;

        var items = await _db.Set<CheckoutItem>()
            .Where(x => x.CheckoutId == checkoutId)
            .Select(x => new { x.Quantity, x.Product.Price, x.Product.TaxClassId })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            if (item.TaxClassId.HasValue)
            {
                var taxRate = await _taxService.GetTaxPercentAsync(item.TaxClassId, countryId, stateOrProvinceId, zipCode, cancellationToken);
                taxAmount += item.Quantity * item.Price * taxRate / 100;
            }
        }

        return taxAmount;
    }

    private async Task<CouponValidationResult> CheckForDiscountIfAnyAsync(
        Checkout checkout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(checkout.CouponCode))
        {
            return new CouponValidationResult { Succeeded = true, DiscountAmount = 0 };
        }

        var cartInfo = new CartInfoForCoupon
        {
            Items = checkout.CheckoutItems.Select(x => new CartItemForCoupon { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
        };

        return await _couponService.ValidateAsync(checkout.CustomerId, checkout.CouponCode, cartInfo, cancellationToken);
    }

    private async Task<Result<ShippingPrice>> ValidateShippingMethodAsync(
        string shippingMethodName, OrderAddressInfo shippingAddress, Checkout checkout, CancellationToken cancellationToken)
    {
        var applicable = await _shippingPriceService.GetApplicableShippingPricesAsync(new GetShippingPriceRequest
        {
            OrderAmount = checkout.CheckoutItems.Sum(x => x.Product.Price * x.Quantity),
            ShippingAddress = shippingAddress
        }, cancellationToken);

        var shippingMethod = applicable.FirstOrDefault(x => x.Name == shippingMethodName);
        if (shippingMethod == null)
        {
            return Result.Fail<ShippingPrice>($"Invalid shipping method {shippingMethodName}");
        }

        return Result.Ok(shippingMethod);
    }

    /// <summary>
    /// A random 6-digit tracking code (100000–999999) not already used by another order. The filtered
    /// unique index on <c>Order.TrackingNumber</c> is the authoritative guard; this just avoids the
    /// common collision before saving.
    /// </summary>
    private async Task<string> GenerateUniqueTrackingNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = Random.Shared.Next(100_000, 1_000_000).ToString();
            var exists = await _db.Set<Order>().AnyAsync(o => o.TrackingNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique order tracking number.");
    }

    private static OrderAddress ToOrderAddress(OrderAddressInfo address) => new()
    {
        AddressLine1 = address.AddressLine1,
        AddressLine2 = address.AddressLine2,
        ContactName = address.ContactName,
        CountryId = address.CountryId,
        StateOrProvinceId = address.StateOrProvinceId,
        DistrictId = address.DistrictId,
        City = address.City,
        ZipCode = address.ZipCode,
        Phone = address.Phone
    };
}
