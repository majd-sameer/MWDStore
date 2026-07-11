using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Store.Application.Auth;
using Store.Application.Catalog;
using Store.Application.Catalog.Pricing;
using Store.Application.Common;
using Store.Application.Content;
using Store.Application.Inventory;
using Store.Application.Localization;
using Store.Application.Messaging;
using Store.Application.Orders;
using Store.Application.Payments;
using Store.Application.Payments.Stripe;
using Store.Application.Pricing.Coupons;
using Store.Application.Scheduling;
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

        services.AddScoped<IProductPricingService, ProductPricingService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IContentBlockService, ContentBlockService>();

        // Content language for the current request; set by RequestCultureMiddleware (Store.Api).
        services.AddScoped<RequestCultureContext>();
        services.AddScoped<IRequestCulture>(sp => sp.GetRequiredService<RequestCultureContext>());

        services.AddScoped<ITaxService, TaxService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IShippingPriceService, DbShippingPriceService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderNotificationService, OrderNotificationService>();
        services.AddScoped<IGatewayPaymentService, GatewayPaymentService>();
        services.AddScoped<IRefundService, RefundService>();
        services.TryAddSingleton<IStripeClient, StripeClient>();
        // Host (Store.Api) binds this from configuration; fall back to defaults when it doesn't.
        services.TryAddSingleton(new PaymentsOptions());

        // Scoped (not the previous Null singleton): the email notifier depends on the scoped StoreDbContext.
        services.AddScoped<IProductBackInStockNotifier, EmailProductBackInStockNotifier>();
        services.AddScoped<IStockService, StockService>();

        // Transactional email. EmailOptions is bound from configuration by the host (Store.Api); fall back
        // to placeholder localhost defaults when it doesn't. The transport is the network seam faked in
        // tests; the renderer is stateless. Sender/queue touch the scoped DbContext so they are scoped.
        services.TryAddSingleton(new EmailOptions());
        // Store-owner recipient for order-lifecycle "owner copy" emails. Host (Store.Api) binds this from
        // the AdminUser configuration section; fall back to the placeholder default when it doesn't.
        services.TryAddSingleton(new OwnerNotificationOptions());
        services.TryAddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.TryAddSingleton<IEmailTransport, MailKitEmailTransport>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IEmailQueueService, EmailQueueService>();

        // JwtOptions is bound and registered by the host (Store.Api) from configuration.
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        // MFA login challenges: same signing key as access tokens but a distinct audience, so the
        // bearer pipeline can never accept a challenge as an access token.
        services.AddSingleton<IMfaChallengeService, MfaChallengeService>();
        // Forgot/reset-password orchestration on top of Identity's built-in reset tokens; reuses
        // PaymentsOptions.StorefrontBaseUrl for the reset link (same storefront-origin config as Stripe).
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IWelcomeEmailService, WelcomeEmailService>();

        // ScheduledTaskOptions is bound by the host (Store.Api) from configuration; falls back to
        // all-enabled defaults when the "ScheduledTasks" section is absent.
        services.AddOptions<ScheduledTaskOptions>();
        services.AddScheduledTask<HeartbeatTask>();
        services.AddScheduledTask<EmailQueueDrainTask>();

        return services;
    }
}
