using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Auth;
using Store.Application.Messaging;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// <see cref="WelcomeEmailService"/>: the <c>Customer.Welcome</c> template is enqueued for the right
/// recipient/tokens, a missing email skips silently, and a broken email queue never bubbles up (so
/// registration always succeeds regardless of email health).
/// </summary>
public class WelcomeEmailServiceTests
{
    /// <summary>Fake <see cref="IEmailQueueService"/> that records enqueue calls, or throws on demand.</summary>
    private sealed class FakeEmailQueueService : IEmailQueueService
    {
        public List<(string Template, IReadOnlyDictionary<string, string?> Tokens, string To, string? ToName)> Enqueued { get; } = [];
        public Exception? ThrowOnEnqueue { get; set; }

        public Task<long> EnqueueAsync(
            string templateName, IReadOnlyDictionary<string, string?> tokens, string to, string? toName = null,
            long? emailAccountId = null, int priority = 0, CancellationToken cancellationToken = default)
        {
            if (ThrowOnEnqueue != null)
            {
                throw ThrowOnEnqueue;
            }

            Enqueued.Add((templateName, tokens, to, toName));
            return Task.FromResult((long)Enqueued.Count);
        }

        public Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private static WelcomeEmailService NewService(FakeEmailQueueService queue) =>
        new(queue, NullLogger<WelcomeEmailService>.Instance);

    private static User NewUser(string? email = "sam@example.com", string? fullName = "Sam Buyer") => new()
    {
        Id = 7,
        UserName = email,
        Email = email,
        FullName = fullName!,
        UserGuid = Guid.NewGuid(),
        CreatedOn = DateTimeOffset.UtcNow,
        LatestUpdatedOn = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SendWelcomeEmailAsync_EnqueuesTemplate_WithRecipientAndNameToken()
    {
        var queue = new FakeEmailQueueService();
        var service = NewService(queue);

        await service.SendWelcomeEmailAsync(NewUser("sam@example.com", "Sam Buyer"));

        var sent = Assert.Single(queue.Enqueued);
        Assert.Equal("Customer.Welcome", sent.Template);
        Assert.Equal("sam@example.com", sent.To);
        Assert.Equal("Sam Buyer", sent.ToName);
        Assert.Equal("Sam Buyer", sent.Tokens["Customer.Name"]);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_MissingFullName_FallsBackToCustomer()
    {
        var queue = new FakeEmailQueueService();
        var service = NewService(queue);

        await service.SendWelcomeEmailAsync(NewUser("sam@example.com", fullName: ""));

        var sent = Assert.Single(queue.Enqueued);
        Assert.Equal("Customer", sent.Tokens["Customer.Name"]);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_NoEmailOnFile_SkipsSilently()
    {
        var queue = new FakeEmailQueueService();
        var service = NewService(queue);

        await service.SendWelcomeEmailAsync(NewUser(email: null));

        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_EnqueueFailure_IsSwallowed_NotThrown()
    {
        var queue = new FakeEmailQueueService { ThrowOnEnqueue = new InvalidOperationException("smtp down") };
        var service = NewService(queue);

        // Must not throw despite the queue failing — registration must still succeed.
        await service.SendWelcomeEmailAsync(NewUser());
    }
}
