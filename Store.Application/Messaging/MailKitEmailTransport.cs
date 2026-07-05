using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Store.Application.Messaging;

/// <summary>
/// The production <see cref="IEmailTransport"/>: sends via MailKit's <see cref="SmtpClient"/> over a real
/// SMTP connection. Chooses implicit SSL vs. STARTTLS from <see cref="EmailMessage.EnableSsl"/>, and
/// authenticates only when a username is supplied (anonymous relays are supported).
/// </summary>
public sealed class MailKitEmailTransport : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(message.FromName, message.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToEmail, message.ToEmail));

        foreach (var bcc in SplitAddresses(message.Bcc))
        {
            mime.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Body }.ToMessageBody();

        using var client = new SmtpClient();
        var secureOption = message.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await client.ConnectAsync(message.Host, message.Port, secureOption, cancellationToken);

        if (!string.IsNullOrWhiteSpace(message.Username))
        {
            await client.AuthenticateAsync(message.Username, message.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static IEnumerable<string> SplitAddresses(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        foreach (var part in raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
}
