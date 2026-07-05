namespace Store.Application.Messaging;

/// <summary>
/// The low-level SMTP transport. This is the seam that isolates the network from the rest of the
/// application: the production implementation (<see cref="MailKitEmailTransport"/>) opens a real SMTP
/// connection, while tests supply a fake that captures or fails on demand. All queue/send orchestration
/// lives above this interface so it can be exercised without a mail server.
/// </summary>
public interface IEmailTransport
{
    /// <summary>Delivers a single resolved message. Throws on any transport failure.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
