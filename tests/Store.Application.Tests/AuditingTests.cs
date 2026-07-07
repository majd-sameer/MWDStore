using Microsoft.EntityFrameworkCore;
using Store.Application.Auditing;
using Store.Data;
using Store.Data.Auditing;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Covers the audit capture pipeline: the DbContext snapshots changed scalar properties into the
/// scoped buffer (secrets stripped, the audit table itself never re-audited), and the service
/// persists append-only rows stamped from the injected clock.
/// </summary>
public class AuditingTests
{
    private static (StoreDbContext Db, AuditContext Audit) NewDb()
    {
        var audit = new AuditContext();
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase("audit-" + Guid.NewGuid())
            .Options;
        return (new StoreDbContext(options, audit), audit);
    }

    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("passwordHash")]
    [InlineData("RefreshTokenHash")]
    [InlineData("SecurityStamp")]
    [InlineData("ConcurrencyStamp")]
    [InlineData("ApiKey")]
    [InlineData("ResetToken")]
    public void IsSecret_true_for_credential_properties(string name)
        => Assert.True(AuditSecrets.IsSecret(name));

    [Theory]
    [InlineData("Name")]
    [InlineData("Email")]
    [InlineData("Price")]
    [InlineData("IsPublished")]
    public void IsSecret_false_for_normal_properties(string name)
        => Assert.False(AuditSecrets.IsSecret(name));

    [Fact]
    public void Capture_added_entity_records_new_values()
    {
        var (db, audit) = NewDb();
        db.Brands.Add(new Brand { Name = "Acme", Slug = "acme", IsPublished = true });
        db.SaveChanges();

        var change = Assert.Single(audit.Changes);
        Assert.Equal("Brand", change.EntityType);
        Assert.Equal("Added", change.State);
        Assert.Equal("Acme", change.EntityName);
        Assert.Contains("Name", change.NewValues.Keys);
        Assert.Empty(change.OldValues);
    }

    [Fact]
    public void Capture_modified_entity_records_only_changed_properties()
    {
        var (db, audit) = NewDb();
        var brand = new Brand { Name = "Acme", Slug = "acme", IsPublished = false };
        db.Brands.Add(brand);
        db.SaveChanges();
        audit.Clear();

        brand.IsPublished = true;
        db.SaveChanges();

        var change = Assert.Single(audit.Changes);
        Assert.Equal("Modified", change.State);
        Assert.Equal("IsPublished", Assert.Single(change.NewValues.Keys));
        Assert.True((bool)change.NewValues["IsPublished"]!);
        Assert.False((bool)change.OldValues["IsPublished"]!);
    }

    [Fact]
    public void Capture_excludes_secret_properties()
    {
        var (db, audit) = NewDb();
        var user = new User
        {
            UserName = "jo",
            Email = "jo@example.com",
            FullName = "Jo",
            PasswordHash = "hash-1",
            SecurityStamp = "stamp-1",
            UserGuid = Guid.NewGuid(),
        };
        db.Users.Add(user);
        db.SaveChanges();
        audit.Clear();

        user.PasswordHash = "hash-2";
        user.FullName = "Joanne";
        db.SaveChanges();

        var change = audit.Changes.Single(c => c.EntityType == "User");
        Assert.Contains("FullName", change.NewValues.Keys);
        Assert.DoesNotContain("PasswordHash", change.NewValues.Keys);
        Assert.DoesNotContain("PasswordHash", change.OldValues.Keys);
    }

    [Fact]
    public void Capture_never_audits_the_audit_table_itself()
    {
        var (db, audit) = NewDb();
        db.AuditLogs.Add(new AuditLog
        {
            UserName = "system",
            Role = string.Empty,
            Action = "Create",
            EntityType = "Brand",
            Area = "Catalog",
            CreatedOn = DateTime.UtcNow,
        });
        db.SaveChanges();

        Assert.Empty(audit.Changes);
    }

    [Fact]
    public async Task LogAsync_writes_a_row_stamped_from_the_clock()
    {
        var (db, _) = NewDb();
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var service = new AuditService(db, new FixedTimeProvider(now));

        await service.LogAsync(new AuditEntry
        {
            Action = "Create",
            EntityType = "Brand",
            Area = "Catalog",
            UserName = "admin",
            EntityName = "Acme",
        });

        var row = Assert.Single(db.AuditLogs);
        Assert.Equal("Create", row.Action);
        Assert.Equal("Acme", row.EntityName);
        Assert.Equal(now.UtcDateTime, row.CreatedOn);
    }

    [Fact]
    public void AuditService_write_surface_is_append_only()
    {
        // Exactly one operation is exposed — no delete/purge — so the trail can only ever grow.
        var methods = typeof(IAuditService).GetMethods();
        Assert.Single(methods);
        Assert.Equal(nameof(IAuditService.LogAsync), methods[0].Name);
    }
}
