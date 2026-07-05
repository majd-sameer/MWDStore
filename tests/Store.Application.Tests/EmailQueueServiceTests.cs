using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Messaging;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Enqueue-then-drain behaviour of the email queue: rendering into a <see cref="QueuedEmail"/>, sending
/// via a fake transport, and the <c>SentTries</c>/<c>MaxTries</c> retry + <c>LastError</c> policy.
/// </summary>
public class EmailQueueServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Fake <see cref="IEmailTransport"/> that records sends and optionally fails a set number of times.</summary>
    private sealed class FakeTransport : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];
        public int FailTimes { get; set; }
        public string FailureMessage { get; set; } = "smtp exploded";
        private int _calls;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls <= FailTimes)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static readonly EmailOptions Options = new()
    {
        FromEmail = "no-reply@mystore.local",
        FromName = "MyStore",
        MaxTries = 3,
        BatchSize = 50
    };

    private static StoreDbContext SeedWithTemplateAndAccount()
    {
        var db = TestDb.New();
        db.EmailAccounts.Add(new EmailAccount
        {
            Id = 1,
            Host = "localhost",
            Port = 25,
            Email = "no-reply@mystore.local",
            DisplayName = "MyStore",
            IsDefault = true
        });
        db.MessageTemplates.Add(new MessageTemplate
        {
            Id = 1,
            Name = "Order.Placed",
            Subject = "Order %Order.Number% confirmed",
            Body = "Hello %Customer.FullName%, thanks for order %Order.Number%.",
            IsActive = true,
            BccEmailAddresses = "audit@mystore.local"
        });
        db.SaveChanges();
        return db;
    }

    private static EmailQueueService NewService(StoreDbContext db, FakeTransport transport)
    {
        var timeProvider = new FixedTimeProvider(Now);
        var sender = new EmailSender(db, transport, Options);
        return new EmailQueueService(
            db,
            new TemplateRenderer(),
            sender,
            Options,
            timeProvider,
            NullLogger<EmailQueueService>.Instance);
    }

    private static IReadOnlyDictionary<string, string?> Tokens() => new Dictionary<string, string?>
    {
        ["Order.Number"] = "ORD-42",
        ["Customer.FullName"] = "Sam"
    };

    [Fact]
    public async Task EnqueueAsync_RendersTokens_AndCreatesQueuedEmail()
    {
        using var db = SeedWithTemplateAndAccount();
        var service = NewService(db, new FakeTransport());

        var id = await service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com", "Sam");

        var queued = Assert.Single(db.QueuedEmails);
        Assert.Equal(id, queued.Id);
        Assert.Equal("sam@example.com", queued.To);
        Assert.Equal("Sam", queued.ToName);
        Assert.Equal("Order ORD-42 confirmed", queued.Subject);
        Assert.Equal("Hello Sam, thanks for order ORD-42.", queued.Body);
        Assert.Equal("audit@mystore.local", queued.Bcc);      // copied from the template
        Assert.Equal(Now, queued.CreatedOn);
        Assert.Null(queued.SentOn);
        Assert.Equal(0, queued.SentTries);
        Assert.Equal(Options.MaxTries, queued.MaxTries);
    }

    [Fact]
    public async Task EnqueueAsync_SuppliesStoreNameToken_WithoutCallerProvidingIt()
    {
        using var db = SeedWithTemplateAndAccount();
        db.MessageTemplates.Add(new MessageTemplate
        {
            Id = 2,
            Name = "Customer.PasswordReset",
            Subject = "Reset your %Store.Name% password",
            Body = "The %Store.Name% team",
            IsActive = true
        });
        db.SaveChanges();
        var service = NewService(db, new FakeTransport());

        await service.EnqueueAsync(
            "Customer.PasswordReset", new Dictionary<string, string?>(), "sam@example.com");

        var queued = Assert.Single(db.QueuedEmails);
        Assert.Equal("Reset your MyStore password", queued.Subject);   // from EmailOptions.FromName
        Assert.Equal("The MyStore team", queued.Body);
    }

    [Fact]
    public async Task EnqueueAsync_Throws_WhenTemplateMissing()
    {
        using var db = SeedWithTemplateAndAccount();
        var service = NewService(db, new FakeTransport());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnqueueAsync("Does.Not.Exist", Tokens(), "sam@example.com"));
    }

    [Fact]
    public async Task EnqueueAsync_Throws_WhenTemplateInactive()
    {
        using var db = SeedWithTemplateAndAccount();
        db.MessageTemplates.Single().IsActive = false;
        db.SaveChanges();
        var service = NewService(db, new FakeTransport());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com"));
    }

    [Fact]
    public async Task ProcessQueueAsync_SendsViaTransport_AndMarksSentOn()
    {
        using var db = SeedWithTemplateAndAccount();
        var transport = new FakeTransport();
        var service = NewService(db, transport);
        await service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com", "Sam");

        var sent = await service.ProcessQueueAsync();

        Assert.Equal(1, sent);
        var message = Assert.Single(transport.Sent);
        Assert.Equal("sam@example.com", message.ToEmail);
        Assert.Equal("no-reply@mystore.local", message.FromEmail); // resolved from the default account
        Assert.Equal("audit@mystore.local", message.Bcc);

        var queued = db.QueuedEmails.Single();
        Assert.Equal(Now, queued.SentOn);
        Assert.Equal(1, queued.SentTries);
        Assert.Null(queued.LastError);
    }

    [Fact]
    public async Task ProcessQueueAsync_IsIdempotent_DoesNotResendAlreadySent()
    {
        using var db = SeedWithTemplateAndAccount();
        var transport = new FakeTransport();
        var service = NewService(db, transport);
        await service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com");

        await service.ProcessQueueAsync();
        var secondPass = await service.ProcessQueueAsync();

        Assert.Equal(0, secondPass);
        Assert.Single(transport.Sent); // still only one send total
    }

    [Fact]
    public async Task ProcessQueueAsync_FailedSend_RecordsLastError_AndLeavesRetryable()
    {
        using var db = SeedWithTemplateAndAccount();
        var transport = new FakeTransport { FailTimes = 1, FailureMessage = "connection refused" };
        var service = NewService(db, transport);
        await service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com");

        var sent = await service.ProcessQueueAsync();

        Assert.Equal(0, sent);
        var queued = db.QueuedEmails.Single();
        Assert.Null(queued.SentOn);
        Assert.Equal(1, queued.SentTries);
        Assert.Equal("connection refused", queued.LastError);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task ProcessQueueAsync_RetriesUntilSuccess_ThenStamped()
    {
        using var db = SeedWithTemplateAndAccount();
        var transport = new FakeTransport { FailTimes = 1 }; // first attempt fails, second succeeds
        var service = NewService(db, transport);
        await service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com");

        await service.ProcessQueueAsync(); // attempt 1 -> fails
        var secondPass = await service.ProcessQueueAsync(); // attempt 2 -> succeeds

        Assert.Equal(1, secondPass);
        var queued = db.QueuedEmails.Single();
        Assert.Equal(Now, queued.SentOn);
        Assert.Equal(2, queued.SentTries);
        Assert.Null(queued.LastError); // cleared on the successful attempt
    }

    [Fact]
    public async Task ProcessQueueAsync_StopsRetrying_OnceMaxTriesReached()
    {
        using var db = SeedWithTemplateAndAccount();
        var transport = new FakeTransport { FailTimes = 100 }; // always fails
        var service = NewService(db, transport);
        await service.EnqueueAsync("Order.Placed", Tokens(), "sam@example.com");

        // MaxTries = 3: three processing passes each consume one try, the fourth finds nothing pending.
        await service.ProcessQueueAsync();
        await service.ProcessQueueAsync();
        await service.ProcessQueueAsync();
        var fourthPass = await service.ProcessQueueAsync();

        var queued = db.QueuedEmails.Single();
        Assert.Equal(Options.MaxTries, queued.SentTries); // capped at MaxTries, not incremented further
        Assert.Equal(0, fourthPass);
        Assert.Null(queued.SentOn);
        Assert.NotNull(queued.LastError);
    }
}
