using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Store.Domain;

namespace Store.Application.Tests;


public class PasswordHashCompatibilityTests
{
    private static readonly User User = new() { Id = 1, UserName = "buyer@example.com" };

    [Fact]
    public void DefaultHasher_RoundTrips_AndRejectsWrongPassword()
    {
        var hasher = new PasswordHasher<User>();
        var hash = hasher.HashPassword(User, "Test@1234");

        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(User, hash, "Test@1234"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(User, hash, "wrong"));
    }

    [Fact]
    public void DefaultHasher_VerifiesHash_ProducedWithDifferentIterationCount()
    {
        // Simulate a hash created by a differently-configured (e.g. older SimplCommerce) Identity install.
        var legacyHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = 310_000
        }));
        var storedHash = legacyHasher.HashPassword(User, "Test@1234");

        // The default hasher reads the parameters embedded in the hash, so verification still succeeds
        // (Success, or SuccessRehashNeeded when the embedded iteration count differs from the default).
        var defaultHasher = new PasswordHasher<User>();
        Assert.NotEqual(PasswordVerificationResult.Failed,
            defaultHasher.VerifyHashedPassword(User, storedHash, "Test@1234"));
    }
}
