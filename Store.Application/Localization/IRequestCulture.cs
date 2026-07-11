using Store.Domain;

namespace Store.Application.Localization;

/// <summary>The content language for the current request. Scoped; defaults to Arabic and is set once
/// per request by RequestCultureMiddleware from Accept-Language ("en*" -> English, else Arabic).</summary>
public interface IRequestCulture { ContentLanguage Language { get; } }

/// <summary>Mutable holder registered scoped; the middleware writes it, everyone else reads IRequestCulture.</summary>
public sealed class RequestCultureContext : IRequestCulture
{
    public ContentLanguage Language { get; set; } = ContentLanguage.Arabic;
}
