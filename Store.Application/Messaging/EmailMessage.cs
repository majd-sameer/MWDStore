namespace Store.Application.Messaging;

/// <summary>
/// A fully-resolved outbound message handed to an <see cref="IEmailTransport"/>. All fields are final:
/// tokens are already rendered and the sending account has already been selected.
/// </summary>
public sealed class EmailMessage
{
    public required string FromEmail { get; init; }
    public required string FromName { get; init; }
    public required string ToEmail { get; init; }
    public string? ToName { get; init; }
    public string? Bcc { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }

    // --- SMTP transport settings (from the chosen EmailAccount, or the EmailOptions fallback) ---
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required bool EnableSsl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}
