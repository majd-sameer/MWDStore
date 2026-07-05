using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Store.Application.Messaging;

/// <summary>
/// Default <see cref="ITemplateRenderer"/>. Scans for <c>%Token.Name%</c> occurrences and replaces each
/// with its value from the supplied dictionary; placeholders whose token is not supplied are left verbatim.
/// A single regex pass avoids ordering pitfalls (a replacement value that itself looks like a token is not
/// re-expanded).
/// </summary>
public sealed partial class TemplateRenderer : ITemplateRenderer
{
    // Token name: letters, digits, dot and underscore between percent signs, e.g. %Order.Number%.
    [GeneratedRegex(@"%([A-Za-z0-9_.]+)%", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();

    public string Render(string template, IReadOnlyDictionary<string, string?> tokens)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var lookup = new Dictionary<string, string?>(tokens, StringComparer.OrdinalIgnoreCase);

        return TokenPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return lookup.TryGetValue(key, out var value)
                ? value ?? string.Empty
                : match.Value; // unknown token: leave the literal placeholder intact
        });
    }
}
