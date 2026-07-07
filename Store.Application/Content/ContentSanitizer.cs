using System.Text.RegularExpressions;

namespace Store.Application.Content;

/// <summary>
/// Minimal server-side sanitizer for <c>richtext</c> content blocks. Keeps "text, not design" true and
/// blocks XSS: script/style blocks are dropped whole, and only a tiny inline whitelist
/// (bold/italic/line-break/paragraph) survives — every other tag and all attributes are stripped, so
/// a value like <c>&lt;script&gt;…&lt;/script&gt;</c> or <c>&lt;a onclick=…&gt;</c> can never reach the DOM.
/// </summary>
public static partial class ContentSanitizer
{
    private static readonly HashSet<string> Allowed =
        new(StringComparer.OrdinalIgnoreCase) { "b", "strong", "i", "em", "br", "p" };

    public static string? Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        // Drop <script>/<style> blocks entirely (tag + inner content), then rebuild the remaining
        // whitelisted tags as bare tags (no attributes) and remove everything else.
        var cleaned = ScriptStyleBlock().Replace(html, string.Empty);

        return Tag().Replace(cleaned, match =>
        {
            var isClosing = match.Groups["close"].Value == "/";
            var name = match.Groups["name"].Value.ToLowerInvariant();

            if (!Allowed.Contains(name))
            {
                return string.Empty;
            }

            return name == "br" ? "<br>" : isClosing ? $"</{name}>" : $"<{name}>";
        });
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleBlock();

    [GeneratedRegex(@"<(?<close>/?)(?<name>[a-zA-Z][a-zA-Z0-9]*)\b[^>]*>")]
    private static partial Regex Tag();
}
