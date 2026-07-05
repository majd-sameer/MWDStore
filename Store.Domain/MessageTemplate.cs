using System;

namespace Store.Domain;

/// <summary>
/// A reusable transactional message template. The <see cref="Subject"/> and <see cref="Body"/> carry
/// token placeholders in <c>%Token.Name%</c> syntax (see <c>ITemplateRenderer</c>) which are replaced at
/// send time. Modeled on nopCommerce's <c>MessageTemplate</c>.
/// </summary>
public class MessageTemplate
{
    public long Id { get; set; }

    /// <summary>Unique lookup key (e.g. <c>Customer.PasswordReset</c>, <c>Order.Placed</c>).</summary>
    public string Name { get; set; } = null!;

    /// <summary>Subject line, may contain <c>%Token.Name%</c> placeholders.</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Body (HTML or text), may contain <c>%Token.Name%</c> placeholders.</summary>
    public string Body { get; set; } = null!;

    /// <summary>When false the template is disabled and enqueue attempts are rejected.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional comma/semicolon-separated list of BCC recipients applied to every send.</summary>
    public string? BccEmailAddresses { get; set; }
}
