using Microsoft.AspNetCore.Http;
using Store.Api.Infrastructure;
using Store.Application.Localization;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers <see cref="RequestCultureMiddleware"/>: it writes the scoped <see cref="RequestCultureContext"/>
/// from the Accept-Language header. The storefront/admin interceptors send a bare "en" or "ar"; this also
/// covers a full BCP-47 value with a quality parameter, and the no-header default.
/// </summary>
public class RequestCultureMiddlewareTests
{
    private static async Task<ContentLanguage> RunAsync(string? acceptLanguage)
    {
        var culture = new RequestCultureContext();
        var middleware = new RequestCultureMiddleware(_ => Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        if (acceptLanguage is not null)
        {
            httpContext.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        await middleware.InvokeAsync(httpContext, culture);
        return culture.Language;
    }

    [Fact]
    public async Task BareEnglish_SetsEnglish()
    {
        Assert.Equal(ContentLanguage.English, await RunAsync("en"));
    }

    [Fact]
    public async Task EnglishWithArabicFallbackQuality_StillPrefersEnglish()
    {
        // The first entry wins per the middleware's simple prefix rule, regardless of q-values.
        Assert.Equal(ContentLanguage.English, await RunAsync("en-US,ar;q=0.8"));
    }

    [Fact]
    public async Task BareArabic_SetsArabic()
    {
        Assert.Equal(ContentLanguage.Arabic, await RunAsync("ar"));
    }

    [Fact]
    public async Task MissingHeader_DefaultsToArabic()
    {
        Assert.Equal(ContentLanguage.Arabic, await RunAsync(null));
    }

    [Fact]
    public async Task NextDelegate_IsInvoked()
    {
        var culture = new RequestCultureContext();
        var invoked = false;
        var middleware = new RequestCultureMiddleware(_ => { invoked = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(new DefaultHttpContext(), culture);

        Assert.True(invoked);
    }
}
