using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Store.Application.Auth;
using Store.Application.Messaging;
using Store.Application.Payments;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Forgot/reset-password orchestration: no account enumeration on request, reset link contains the
/// Identity token, best-effort email enqueue, and a successful reset both changes the password and
/// revokes the stored refresh token.
/// </summary>
public class PasswordResetServiceTests
{
    /// <summary>Fake <see cref="IEmailQueueService"/> that records enqueue calls instead of touching a DbContext.</summary>
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

    private static readonly PaymentsOptions PaymentsOptions = new() { StorefrontBaseUrl = "http://localhost:4200" };

    /// <summary>
    /// Builds a fully working <see cref="UserManager{TUser}"/> backed by the given (EF InMemory)
    /// <see cref="StoreDbContext"/>, wired the same way Store.Api's Program.cs wires it (AddIdentityCore +
    /// AddEntityFrameworkStores + AddDefaultTokenProviders), so GeneratePasswordResetTokenAsync /
    /// ResetPasswordAsync behave exactly as they do at runtime.
    /// </summary>
    private static UserManager<User> NewUserManager(StoreDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        services.AddDataProtection();
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredUniqueChars = 0;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<StoreDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider().GetRequiredService<UserManager<User>>();
    }

    private static PasswordResetService NewService(
        UserManager<User> userManager, FakeEmailQueueService emailQueue) =>
        new(
            userManager,
            emailQueue,
            new RefreshTokenService(new JwtOptions { Key = "unit-test-signing-key-which-is-long-enough-32+chars" }, TimeProvider.System),
            PaymentsOptions,
            NullLogger<PasswordResetService>.Instance);

    private static async Task<User> CreateUserAsync(UserManager<User> userManager, string email, string password)
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            FullName = "Sam Buyer",
            UserGuid = Guid.NewGuid(),
            CreatedOn = DateTimeOffset.UtcNow,
            LatestUpdatedOn = DateTimeOffset.UtcNow
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    [Fact]
    public async Task RequestResetAsync_ExistingUser_EnqueuesEmail_WithTokenInUrl()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        await CreateUserAsync(userManager, "sam@example.com", "Test@1234");
        var emailQueue = new FakeEmailQueueService();
        var service = NewService(userManager, emailQueue);

        await service.RequestResetAsync("sam@example.com");

        var sent = Assert.Single(emailQueue.Enqueued);
        Assert.Equal("Customer.PasswordReset", sent.Template);
        Assert.Equal("sam@example.com", sent.To);
        Assert.Equal("Sam Buyer", sent.ToName);
        Assert.Equal("Sam Buyer", sent.Tokens["Customer.FullName"]);

        var url = sent.Tokens["Customer.PasswordResetUrl"];
        Assert.NotNull(url);
        Assert.StartsWith("http://localhost:4200/reset-password?email=sam%40example.com&token=", url);

        // The token in the URL must be the real Identity reset token (decoded), reusable by ResetPasswordAsync.
        var token = Uri.UnescapeDataString(url!.Split("token=")[1]);
        var user = await userManager.FindByEmailAsync("sam@example.com");
        Assert.True(await userManager.VerifyUserTokenAsync(
            user!, userManager.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", token));
    }

    [Fact]
    public async Task RequestResetAsync_UnknownEmail_DoesNothing_AndDoesNotThrow()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var emailQueue = new FakeEmailQueueService();
        var service = NewService(userManager, emailQueue);

        await service.RequestResetAsync("nobody@example.com");

        Assert.Empty(emailQueue.Enqueued);
    }

    [Fact]
    public async Task RequestResetAsync_EnqueueFailure_IsSwallowed_NotThrown()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        await CreateUserAsync(userManager, "sam@example.com", "Test@1234");
        var emailQueue = new FakeEmailQueueService { ThrowOnEnqueue = new InvalidOperationException("smtp down") };
        var service = NewService(userManager, emailQueue);

        // Must not throw despite the queue failing.
        await service.RequestResetAsync("sam@example.com");
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ChangesPassword_AndRevokesRefreshToken()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var user = await CreateUserAsync(userManager, "sam@example.com", "Test@1234");
        user.RefreshTokenHash = "some-hash";
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        await userManager.UpdateAsync(user);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var emailQueue = new FakeEmailQueueService();
        var service = NewService(userManager, emailQueue);

        var result = await service.ResetPasswordAsync("sam@example.com", token, "NewPass@5678");

        Assert.True(result.Succeeded);
        Assert.True(await userManager.CheckPasswordAsync(
            (await userManager.FindByEmailAsync("sam@example.com"))!, "NewPass@5678"));

        var updated = await userManager.FindByEmailAsync("sam@example.com");
        Assert.Null(updated!.RefreshTokenHash);
        Assert.Null(updated.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_Fails_AndDoesNotChangePassword()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        await CreateUserAsync(userManager, "sam@example.com", "Test@1234");
        var emailQueue = new FakeEmailQueueService();
        var service = NewService(userManager, emailQueue);

        var result = await service.ResetPasswordAsync("sam@example.com", "not-a-real-token", "NewPass@5678");

        Assert.False(result.Succeeded);
        Assert.True(await userManager.CheckPasswordAsync(
            (await userManager.FindByEmailAsync("sam@example.com"))!, "Test@1234"));
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownEmail_FailsCleanly()
    {
        using var db = TestDb.New();
        var userManager = NewUserManager(db);
        var emailQueue = new FakeEmailQueueService();
        var service = NewService(userManager, emailQueue);

        var result = await service.ResetPasswordAsync("nobody@example.com", "whatever", "NewPass@5678");

        Assert.False(result.Succeeded);
    }
}
