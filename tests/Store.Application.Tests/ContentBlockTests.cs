using Store.Application.Content;
using Store.Application.Localization;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Content-block building blocks (Phase 5): the richtext sanitizer keeps a tiny formatting whitelist
/// and blocks scripts/attributes, and the English overlay serves a ContentBlock's translated Value
/// via the same LocalizedContentProperty machinery products use.
/// </summary>
public class ContentBlockTests
{
    [Fact]
    public void Sanitize_strips_scripts_and_unsafe_tags()
    {
        const string input =
            "<p>Hello <b>world</b></p><script>alert('x')</script><a href=\"j\" onclick=\"steal()\">link</a>";

        var output = ContentSanitizer.Sanitize(input);

        Assert.DoesNotContain("script", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", output, StringComparison.OrdinalIgnoreCase); // script content dropped
        Assert.DoesNotContain("onclick", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a", output, StringComparison.OrdinalIgnoreCase);    // anchor tag removed
        Assert.Contains("<b>world</b>", output);                                     // whitelist survives
        Assert.Contains("<p>", output);
        Assert.Contains("link", output);                                             // text kept, tag gone
    }

    [Fact]
    public void Sanitize_keeps_basic_formatting_and_line_breaks()
    {
        var output = ContentSanitizer.Sanitize("<i>a</i><br><strong>b</strong><em>c</em>");

        Assert.Equal("<i>a</i><br><strong>b</strong><em>c</em>", output);
    }

    [Fact]
    public void Sanitize_passes_plain_text_and_null_through()
    {
        Assert.Equal("Just text", ContentSanitizer.Sanitize("Just text"));
        Assert.Null(ContentSanitizer.Sanitize(null));
    }

    [Fact]
    public async Task Overlay_serves_english_value_for_a_content_block()
    {
        using var db = TestDb.New();
        var block = new ContentBlock
        {
            PageKey = "home",
            SectionKey = "hero-grid",
            BlockKey = "hero-copy.title",
            Type = "text",
            Value = "عنوان عربي",
            IsActive = true,
        };
        db.ContentBlocks.Add(block);
        db.SaveChanges();

        db.LocalizedContentProperties.Add(new LocalizedContentProperty
        {
            EntityType = LocalizedEntity.ContentBlock,
            EntityId = block.Id,
            CultureId = "en-US",
            ProperyName = LocalizedProperty.Value,
            Value = "English title",
        });
        db.SaveChanges();

        var service = new LocalizationService(db);

        var english = await service.GetOverlayAsync(LocalizedEntity.ContentBlock, new[] { block.Id }, "en-US");
        Assert.Equal("English title", english.Apply(block.Id, LocalizedProperty.Value, block.Value));

        // Arabic has no overlay row — falls back to the base (Arabic) value.
        var arabic = await service.GetOverlayAsync(LocalizedEntity.ContentBlock, new[] { block.Id }, "arabic");
        Assert.Equal("عنوان عربي", arabic.Apply(block.Id, LocalizedProperty.Value, block.Value));
    }
}
