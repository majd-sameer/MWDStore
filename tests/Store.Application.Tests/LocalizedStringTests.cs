using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the <see cref="LocalizedString"/> value object that replaces the old
/// <c>LocalizedContentProperty</c> overlay: <c>Resolve</c> fallback semantics, <c>From</c>'s
/// empty-to-null normalization (so clearing the English text round-trips), and <c>HasEnglish</c>.
/// </summary>
public class LocalizedStringTests
{
    // ===== Resolve ===================================================================================

    [Fact]
    public void Resolve_Arabic_ReturnsAr()
    {
        var s = new LocalizedString("قميص", "Shirt");
        Assert.Equal("قميص", s.Resolve(ContentLanguage.Arabic));
    }

    [Fact]
    public void Resolve_English_WhenPresent_ReturnsEn()
    {
        var s = new LocalizedString("قميص", "Shirt");
        Assert.Equal("Shirt", s.Resolve(ContentLanguage.English));
    }

    [Fact]
    public void Resolve_English_WhenMissing_FallsBackToAr()
    {
        var s = new LocalizedString("قميص");
        Assert.Equal("قميص", s.Resolve(ContentLanguage.English));
    }

    [Fact]
    public void Resolve_English_NeverReturnsEmptyString_FallsBackToAr()
    {
        // Constructing directly with En = "" bypasses From's normalization; Resolve must still
        // treat empty as "missing" so a stray empty string can never leak out.
        var s = new LocalizedString("قميص") { En = "" };
        Assert.Equal("قميص", s.Resolve(ContentLanguage.English));
    }

    [Fact]
    public void Resolve_BothNull_ReturnsNull()
    {
        var s = new LocalizedString(null, null);
        Assert.Null(s.Resolve(ContentLanguage.Arabic));
        Assert.Null(s.Resolve(ContentLanguage.English));
    }

    // ===== From =======================================================================================

    [Fact]
    public void From_BothPresent_CreatesInstance()
    {
        var s = LocalizedString.From("قميص", "Shirt");
        Assert.NotNull(s);
        Assert.Equal("قميص", s!.Ar);
        Assert.Equal("Shirt", s.En);
    }

    [Fact]
    public void From_EmptyEnglish_NormalizesToNull()
    {
        var s = LocalizedString.From("قميص", "");
        Assert.NotNull(s);
        Assert.Null(s!.En);
    }

    [Fact]
    public void From_BothEmpty_ReturnsNull()
    {
        Assert.Null(LocalizedString.From("", ""));
        Assert.Null(LocalizedString.From(null, null));
    }

    [Fact]
    public void From_OnlyArabic_CreatesInstanceWithNullEnglish()
    {
        var s = LocalizedString.From("قميص", null);
        Assert.NotNull(s);
        Assert.Equal("قميص", s!.Ar);
        Assert.Null(s.En);
    }

    [Fact]
    public void From_OnlyEnglish_CreatesInstanceWithNullArabic()
    {
        var s = LocalizedString.From(null, "Shirt");
        Assert.NotNull(s);
        Assert.Null(s!.Ar);
        Assert.Equal("Shirt", s.En);
    }

    // ===== HasEnglish =================================================================================

    [Fact]
    public void HasEnglish_TrueWhenEnPresent()
    {
        Assert.True(new LocalizedString("قميص", "Shirt").HasEnglish);
    }

    [Fact]
    public void HasEnglish_FalseWhenEnNullOrEmpty()
    {
        Assert.False(new LocalizedString("قميص").HasEnglish);
        Assert.False(new LocalizedString("قميص") { En = "" }.HasEnglish);
    }

    // ===== Null-tolerant extension methods (optional LocalizedString? fields) =======================

    [Fact]
    public void Extension_Resolve_NullInstance_ReturnsNull()
    {
        // Called via the static form: "s.Resolve(...)" on a LocalizedString? local binds to the
        // instance method (nullability is an annotation, not a distinct overload-resolution type),
        // so the null-tolerant extension is only reachable through explicit static dispatch.
        LocalizedString? s = null;
        Assert.Null(LocalizedStringExtensions.Resolve(s, ContentLanguage.Arabic));
        Assert.Null(LocalizedStringExtensions.Resolve(s, ContentLanguage.English));
    }

    [Fact]
    public void Extension_Resolve_NonNullInstance_DelegatesToResolve()
    {
        LocalizedString? s = new("قميص", "Shirt");
        Assert.Equal("Shirt", LocalizedStringExtensions.Resolve(s, ContentLanguage.English));
    }

    [Fact]
    public void Extension_HasEnglish_NullInstance_ReturnsFalse()
    {
        LocalizedString? s = null;
        Assert.False(s.HasEnglish());
    }

    [Fact]
    public void Extension_HasEnglish_NonNullInstance_DelegatesToHasEnglish()
    {
        LocalizedString? s = new("قميص", "Shirt");
        Assert.True(s.HasEnglish());
    }
}
