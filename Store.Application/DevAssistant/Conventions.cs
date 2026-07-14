using System.Text;

namespace Store.Application.DevAssistant;

/// <summary>
/// The codebase's naming conventions, encoded so cross-layer file correlations are computed, not
/// hand-maintained (spec §2.3 Source C). Paths are repository-relative — never server filesystem
/// paths (SEC-11).
/// </summary>
public static class Conventions
{
    public static string Pluralize(string name)
    {
        if (name.EndsWith("y", StringComparison.Ordinal) && name.Length > 1 && !IsVowel(name[^2]))
            return name[..^1] + "ies";
        if (name.EndsWith("s", StringComparison.Ordinal) || name.EndsWith("x", StringComparison.Ordinal)
            || name.EndsWith("ch", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal))
            return name + "es";
        return name + "s";
    }

    private static bool IsVowel(char c) => "aeiouAEIOU".Contains(c);

    public static string KebabCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    builder.Append('-');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    public static string KebabPlural(string name) => KebabCase(Pluralize(name));

    /// <summary>The convention-derived artifact set for an entity, graded verified/expected.</summary>
    public static IReadOnlyList<CorrelatedArtifact> ArtifactsFor(EntitySnapshot entity)
    {
        var plural = Pluralize(entity.ClrName);
        var segment = entity.AdminAreaSegment ?? KebabPlural(entity.ClrName);

        // Frontend paths are always expected-grade: the API cannot reflect over the Angular
        // workspace, so the convention is pinned and the answer says so (spec §2.3).
        return
        [
            new CorrelatedArtifact("Domain", $"Store.Domain/{entity.ClrName}.cs", true,
                "The entity class (plain C#, no EF Core here)."),
            new CorrelatedArtifact("Data", $"Store.Data/Configurations/{entity.ClrName}Configuration.cs",
                entity.HasConfigurationClass,
                "EF mapping: table name, column types/lengths, indexes, relationships."),
            new CorrelatedArtifact("API", $"Store.Api/Controllers/Admin/Admin{plural}Controller.cs",
                entity.HasAdminController,
                $"The admin controller for /api/admin/{segment} and its DTO projections."),
            new CorrelatedArtifact("API", "Store.Api/Models/AdminModels.cs", false,
                "The DTO records (list/detail/upsert) — the public contract."),
            new CorrelatedArtifact("data-access", "web/projects/data-access/src/lib/models.ts", false,
                "The TypeScript mirror of the DTO records (same commit as the backend change)."),
            new CorrelatedArtifact("data-access", $"web/projects/data-access/src/lib/admin/admin-{segment}.service.ts", false,
                "The typed Angular service for the admin controller."),
            new CorrelatedArtifact("Admin UI", $"web/projects/admin/src/app/features/{segment}/", false,
                "The lazy admin feature area (list + form components, route, sidebar entry).")
        ];
    }
}
