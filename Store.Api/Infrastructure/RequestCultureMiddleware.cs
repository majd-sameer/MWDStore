using Store.Application.Localization;

namespace Store.Api.Infrastructure;

/// <summary>Sets the scoped RequestCultureContext from Accept-Language. The storefront/admin
/// interceptors send bare "en" or "ar"; a missing/other header means Arabic (canonical content).</summary>
public sealed class RequestCultureMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, RequestCultureContext culture)
    {
        if (context.Request.Headers.AcceptLanguage.ToString()
                .StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            culture.Language = Store.Domain.ContentLanguage.English;
        }
        return next(context);
    }
}
