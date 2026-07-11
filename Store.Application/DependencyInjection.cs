using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Store.Application.Auditing;
using Store.Application.Auth;
using Store.Application.Catalog;
using Store.Application.Catalog.Pricing;
using Store.Application.Common;
using Store.Application.Inventory;
using Store.Application.Localization;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Application.Payments.Stripe;
using Store.Application.Pricing.Coupons;
using Store.Application.Shipping;
using Store.Application.ShoppingCart;
using Store.Application.Tax;

namespace Store.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddStoreApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMediaUrlBuilder, LocalMediaUrlBuilder>();
        services.TryAddSingleton(new CatalogOptions());
        services.TryAddSingleton(new ShippingOptions());

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditStampReader, AuditStampReader>();
        services.AddScoped<IProductPricingService, ProductPricingService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<ILocalizedContentWriter, LocalizedContentWriter>();

        services.AddScoped<ITaxService, TaxService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IShippingPriceService, DbShippingPriceService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IGatewayPaymentService, GatewayPaymentService>();
        services.TryAddSingleton<IStripeClient, StripeClient>();
        // Host (Store.Api) binds this from configuration; fall back to defaults when it doesn't.
        services.TryAddSingleton(new PaymentsOptions());

        services.TryAddSingleton<IProductBackInStockNotifier, NullProductBackInStockNotifier>();
        services.AddScoped<IStockService, StockService>();

        // JwtOptions is bound and registered by the host (Store.Api) from configuration.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        return services;
    }
}
