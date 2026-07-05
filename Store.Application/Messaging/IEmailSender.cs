using Store.Domain;

namespace Store.Application.Messaging;

/// <summary>
/// Sends a single email over SMTP. Resolves the sending <see cref="EmailAccount"/> (the one referenced by
/// the queued email, else the default account, else the <see cref="EmailOptions"/> fallback), builds the
/// <see cref="EmailMessage"/>, and hands it to the <see cref="IEmailTransport"/>. Throws on failure so the
/// queue processor can record the error and retry.
/// </summary>
public interface IEmailSender
{
    /// <summary>Resolves the account and delivers the given queued email. Throws on transport failure.</summary>
    Task SendAsync(QueuedEmail email, CancellationToken cancellationToken = default);
}
