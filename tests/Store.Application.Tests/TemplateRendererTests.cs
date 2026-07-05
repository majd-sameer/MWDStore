using Store.Application.Messaging;

namespace Store.Application.Tests;

/// <summary>
/// Token substitution for the <c>%Token.Name%</c> syntax: known tokens are replaced (case-insensitively),
/// unknown tokens are left as the literal placeholder, and a null value renders as empty.
/// </summary>
public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Fact]
    public void Render_ReplacesKnownTokens_InSubjectAndBody()
    {
        var tokens = new Dictionary<string, string?>
        {
            ["Store.Name"] = "MyStore",
            ["Order.Number"] = "ABC123"
        };

        var result = _renderer.Render("Your %Store.Name% order %Order.Number% is confirmed", tokens);

        Assert.Equal("Your MyStore order ABC123 is confirmed", result);
    }

    [Fact]
    public void Render_IsCaseInsensitiveOnTokenName()
    {
        var tokens = new Dictionary<string, string?> { ["Customer.FullName"] = "Sam" };

        var result = _renderer.Render("Hello %customer.fullname%", tokens);

        Assert.Equal("Hello Sam", result);
    }

    [Fact]
    public void Render_LeavesUnknownTokensIntact()
    {
        var tokens = new Dictionary<string, string?> { ["Store.Name"] = "MyStore" };

        var result = _renderer.Render("%Store.Name% - %Unknown.Token%", tokens);

        Assert.Equal("MyStore - %Unknown.Token%", result);
    }

    [Fact]
    public void Render_NullValue_RendersAsEmpty()
    {
        var tokens = new Dictionary<string, string?> { ["Order.Note"] = null };

        var result = _renderer.Render("Note:[%Order.Note%]", tokens);

        Assert.Equal("Note:[]", result);
    }

    [Fact]
    public void Render_DoesNotReExpandReplacementValues()
    {
        // A value that itself looks like a token must not be re-expanded in a second pass.
        var tokens = new Dictionary<string, string?>
        {
            ["A"] = "%B%",
            ["B"] = "boom"
        };

        var result = _renderer.Render("%A%", tokens);

        Assert.Equal("%B%", result);
    }
}
