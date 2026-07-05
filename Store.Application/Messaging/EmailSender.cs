using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Application.Messaging;

/// <summary>
/// Default <see cref="IEmailSender"/>. Selects the sending account and delegates the actual delivery to the
/// injected <see cref="IEmailTransport"/> (faked in tests). Account resolution order:
/// <list type="number">
///   <item>the <see cref="QueuedEmail.EmailAccountId"/> if set,</item>
///   <item>otherwise the default <see cref="EmailAccount"/> (<see cref="EmailAccount.IsDefault"/>),</item>
///   <item>otherwise the <see cref="EmailOptions"/> host fallback.</item>
/// </list>
/// </summary>
public sealed class EmailSender : IEmailSender
{
    private readonly StoreDbContext _db;
    private readonly IEmailTransport _transport;
    private readonly EmailOptions _options;

    public EmailSender(StoreDbContext db, IEmailTransport transport, EmailOptions options)
    {
        _db = db;
        _transport = transport;
        _options = options;
    }

    public async Task SendAsync(QueuedEmail email, CancellationToken cancellationToken = default)
    {
        var account = await ResolveAccountAsync(email, cancellationToken);

        var message = new EmailMessage
        {
            FromEmail = account?.Email ?? _options.FromEmail,
            FromName = account?.DisplayName ?? _options.FromName,
            ToEmail = email.To,
            ToName = email.ToName,
            Bcc = email.Bcc,
            Subject = email.Subject,
            Body = email.Body,
            Host = account?.Host ?? _options.Host,
            Port = account?.Port ?? _options.Port,
            EnableSsl = account?.EnableSsl ?? _options.EnableSsl,
            Username = account?.Username ?? _options.Username,
            Password = account?.Password ?? _options.Password
        };

        await _transport.SendAsync(message, cancellationToken);
    }

    private async Task<EmailAccount?> ResolveAccountAsync(QueuedEmail email, CancellationToken cancellationToken)
    {
        if (email.EmailAccountId is { } accountId)
        {
            return await _db.EmailAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        }

        return await _db.EmailAccounts.FirstOrDefaultAsync(a => a.IsDefault, cancellationToken);
    }
}
