using System.Collections.Generic;

namespace Store.Application.Messaging;

/// <summary>
/// Replaces token placeholders in a template's subject/body with runtime values.
/// <para>
/// Token syntax is <c>%Token.Name%</c> (nopCommerce style). A token is matched case-insensitively by the
/// name between the percent signs; unknown tokens are left untouched (so template authors see the literal
/// placeholder rather than an empty gap when a value is missing). To emit a literal percent sign, no
/// escaping is needed unless the text happens to form <c>%KnownToken%</c>.
/// </para>
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>Substitutes <c>%Token.Name%</c> placeholders in <paramref name="template"/> using
    /// <paramref name="tokens"/> (keys are matched without the surrounding percent signs, case-insensitively).</summary>
    string Render(string template, IReadOnlyDictionary<string, string?> tokens);
}
