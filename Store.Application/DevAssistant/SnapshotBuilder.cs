using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Store.Data.Auditing;
using Store.Application.Localization;

namespace Store.Application.DevAssistant;

/// <summary>
/// Pure translation of an EF <see cref="IModel"/> + a reflected API surface into the immutable
/// <see cref="SystemMetadataSnapshot"/>. Kept free of DI so tests can build snapshots from any
/// model (the self-discovery guarantee, spec §3.5 / TEST-2).
/// </summary>
public static class SnapshotBuilder
{
    /// <summary>CLR names of entities participating in the bilingual overlay system (spec §2.3 Source A).</summary>
    private static readonly HashSet<string> BilingualEntities = new(StringComparer.Ordinal)
    {
        LocalizedEntity.Product, LocalizedEntity.NewsItem, LocalizedEntity.NewsCategory,
        LocalizedEntity.ContentBlock, LocalizedEntity.Category, LocalizedEntity.Brand,
        LocalizedEntity.Vendor, LocalizedEntity.Page, LocalizedEntity.Menu, LocalizedEntity.MenuItem,
        LocalizedEntity.ProductAttribute, LocalizedEntity.ProductAttributeGroup,
        LocalizedEntity.ProductOption, LocalizedEntity.CartRule, LocalizedEntity.Country,
        LocalizedEntity.StateOrProvince
    };

    public static SystemMetadataSnapshot Build(
        IModel model,
        IReadOnlyList<ApiEndpointDescriptor> endpoints,
        string assemblyVersion,
        DateTimeOffset builtAt,
        bool? hasPendingMigrations,
        IReadOnlyList<string> notices,
        Assembly? configurationsAssembly = null)
    {
        var allNotices = new List<string>(notices);
        var configurationNames = ScanConfigurationClasses(configurationsAssembly, allNotices);
        var entities = new List<EntitySnapshot>();

        foreach (var entityType in model.GetEntityTypes()
                     .Where(e => !e.IsOwned())
                     .OrderBy(e => e.ClrType.Name, StringComparer.Ordinal))
        {
            try
            {
                entities.Add(BuildEntity(entityType, endpoints, configurationNames));
            }
            catch (Exception ex)
            {
                // SEC-15: one bad entity degrades that answer domain, never the snapshot.
                allNotices.Add($"Entity '{entityType.ClrType.Name}' could not be read from the EF model: {ex.Message}");
            }
        }

        var fingerprint = new SnapshotFingerprint(assemblyVersion, ComputeModelHash(entities), builtAt);
        return new SystemMetadataSnapshot(fingerprint, entities, endpoints, hasPendingMigrations, allNotices);
    }

    private static EntitySnapshot BuildEntity(
        IEntityType entityType,
        IReadOnlyList<ApiEndpointDescriptor> endpoints,
        IReadOnlySet<string> configurationNames)
    {
        var clrType = entityType.ClrType;
        var tableName = entityType.GetTableName() ?? clrType.Name;

        var properties = new List<PropertySnapshot>();
        foreach (var property in entityType.GetProperties())
        {
            var isSensitive = AuditSecrets.IsSecret(property.Name);
            var firstFk = property.GetContainingForeignKeys().FirstOrDefault();
            var indexes = property.GetContainingIndexes().ToList();

            properties.Add(new PropertySnapshot(
                property.Name,
                FriendlyClrType(property.ClrType),
                // SEC-10: sensitive properties appear by name only; details are suppressed.
                isSensitive ? null : property.GetColumnType(),
                isSensitive ? null : property.GetMaxLength(),
                property.IsNullable,
                isSensitive ? null : property.GetDefaultValue()?.ToString() ?? property.GetDefaultValueSql(),
                property.IsPrimaryKey(),
                firstFk is not null,
                firstFk?.PrincipalEntityType.ClrType.Name,
                indexes.Count > 0,
                indexes.Any(i => i.IsUnique),
                isSensitive));
        }

        var navigations = entityType.GetNavigations()
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .Select(n => new NavigationSnapshot(
                n.Name,
                n.TargetEntityType.ClrType.Name,
                n.IsCollection ? "one-to-many" : "many-to-one",
                n.ForeignKey.DeleteBehavior.ToString()))
            .ToList();

        var indexSnapshots = entityType.GetIndexes()
            .Select(i => new IndexSnapshot(
                i.GetDatabaseName(),
                i.Properties.Select(p => p.Name).ToList(),
                i.IsUnique,
                i.GetFilter()))
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        var adminSegment = FindAdminAreaSegment(clrType.Name, endpoints);

        return new EntitySnapshot(
            clrType.Name,
            clrType.Namespace ?? string.Empty,
            tableName,
            Implements(clrType, "ISoftDeletable"),
            Implements(clrType, "ISeoEntity"),
            Implements(clrType, "IAuditedEntity"),
            BilingualEntities.Contains(clrType.Name),
            adminSegment ?? Conventions.KebabPlural(clrType.Name),
            adminSegment is not null,
            configurationNames.Contains(clrType.Name + "Configuration"),
            properties,
            navigations,
            indexSnapshots);
    }

    /// <summary>Marker-interface facts by name so the builder works for synthetic test entities too.</summary>
    private static bool Implements(Type clrType, string interfaceName) =>
        clrType.GetInterfaces().Any(i => i.Name == interfaceName);

    /// <summary>
    /// Confirms the convention-expected admin controller by looking for a reflected route
    /// <c>api/admin/{segment}</c> whose controller is <c>Admin{Plural}</c>.
    /// </summary>
    private static string? FindAdminAreaSegment(string clrName, IReadOnlyList<ApiEndpointDescriptor> endpoints)
    {
        var expectedController = "Admin" + Conventions.Pluralize(clrName);
        var match = endpoints.FirstOrDefault(e =>
            string.Equals(e.Controller, expectedController, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return null;

        var parts = match.Route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && parts[0] == "api" && parts[1] == "admin" ? parts[2] : null;
    }

    private static IReadOnlySet<string> ScanConfigurationClasses(Assembly? assembly, List<string> notices)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (assembly is null)
            return names;
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.Name.EndsWith("Configuration", StringComparison.Ordinal)
                    && type.GetInterfaces().Any(i => i.Name.StartsWith("IEntityTypeConfiguration", StringComparison.Ordinal)))
                {
                    names.Add(type.Name);
                }
            }
        }
        catch (Exception ex)
        {
            notices.Add($"Configuration-class scan failed; file correlations degrade to 'expected': {ex.Message}");
        }
        return names;
    }

    private static string FriendlyClrType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        var name = (underlying ?? type).Name;
        return underlying is null ? name : name + "?";
    }

    /// <summary>Stable hash over the ordered entity/property definitions (spec §2.3).</summary>
    private static string ComputeModelHash(IReadOnlyList<EntitySnapshot> entities)
    {
        var canonical = new StringBuilder();
        foreach (var entity in entities)
        {
            canonical.Append(entity.ClrName).Append('=').Append(entity.TableName).Append(';');
            foreach (var property in entity.Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                canonical.Append(property.Name).Append(':')
                    .Append(property.ClrType).Append(':')
                    .Append(property.SqlType).Append(':')
                    .Append(property.IsNullable ? '1' : '0').Append(',');
            }
            canonical.Append('|');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
