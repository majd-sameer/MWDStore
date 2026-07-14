using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Application.DevAssistant;
using Store.Data;

namespace Store.Application.Tests;

public class DevAssistantTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // A hand-authored API surface standing in for the reflection scan (which lives in Store.Api).
    private static readonly ApiEndpointDescriptor[] Endpoints =
    [
        new("AdminCategories", "List", "GET", "api/admin/categories", "area:catalog", false, false, null, "IReadOnlyList<AdminCategoryDto>"),
        new("AdminCategories", "Create", "POST", "api/admin/categories", "area:catalog", false, false, "CategoryUpsertRequest", "AdminCategoryDto"),
        new("AdminCategories", "Delete", "DELETE", "api/admin/categories/{id:long}", "area:catalog", false, false, null, null),
        new("AdminBrands", "List", "GET", "api/admin/brands", "area:catalog", false, false, null, null),
        new("AdminInventory", "StockOut", "POST", "api/admin/inventory/stock-out", "area:inventory", false, true, "StockOutApiRequest", "ProductStockDto"),
        new("Catalog", "GetProduct", "GET", "api/catalog/products/{id:long}", null, true, false, null, null)
    ];

    private static SystemMetadataSnapshot BuildSnapshot()
    {
        // UseSqlServer supplies the relational type mappings; the model is read without connecting.
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        using var db = new StoreDbContext(options);
        return SnapshotBuilder.Build(db.Model, Endpoints, "1.0.0-test", FixedNow, false, [], typeof(StoreDbContext).Assembly);
    }

    private static readonly Lazy<SystemMetadataSnapshot> Snapshot = new(BuildSnapshot);

    private sealed class FixedMetadataProvider(SystemMetadataSnapshot snapshot) : ISystemMetadataProvider
    {
        public SystemMetadataSnapshot Snapshot { get; } = snapshot;
    }

    private static DevAssistantService NewService() => new(new FixedMetadataProvider(Snapshot.Value));

    // ---------------------------------------------------------------- worked example: categories (§3.4)

    [Fact]
    public void CategoriesChangePath_ResolvesIntentAndSubject()
    {
        var reply = NewService().Query("How do I add a new property to the categories module?");

        Assert.Equal("ChangePathQuery", reply.Intent);
        Assert.Equal("Category", reply.Subject);
        Assert.True(reply.Hit);

        var checklist = Assert.Single(reply.Blocks.OfType<ChecklistBlock>());
        Assert.True(checklist.Interactive);

        // Real, verified paths for the layers reflection can confirm.
        var domainStep = checklist.Steps.First(s => s.Layer == "Domain");
        Assert.Equal("Store.Domain/Category.cs", domainStep.FilePath);
        Assert.True(domainStep.Verified);
        Assert.Contains(checklist.Steps, s => s.FilePath == "Store.Data/Configurations/CategoryConfiguration.cs" && s.Verified);
        Assert.Contains(checklist.Steps, s => s.FilePath == "Store.Api/Controllers/Admin/AdminCategoriesController.cs" && s.Verified);

        // Category is bilingual, so the overlay pair step is included…
        Assert.Contains(checklist.Steps, s => s.Description.Contains("bilingual", StringComparison.OrdinalIgnoreCase));

        // …and the hard rules bite at their steps (spec §3.4 lists HR 1, 2, 5, 8).
        var ruleIds = checklist.Steps.SelectMany(s => s.Warnings).Select(w => w.RuleId).ToHashSet();
        Assert.Superset(new HashSet<string> { "HR-1", "HR-2", "HR-5", "HR-8" }, ruleIds);
    }

    // -------------------------------------------------------------- worked example: departments (§3.5)

    [Fact]
    public void DepartmentsRoutes_IsAnHonestMissWithEscalation()
    {
        var reply = NewService().Query("Show me all routes for departments.");

        Assert.Equal("RouteQuery", reply.Intent);
        Assert.False(reply.Hit);
        Assert.Null(reply.Subject);

        var text = Assert.Single(reply.Blocks.OfType<TextBlock>());
        Assert.Contains("department", text.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Snapshot.Value.Fingerprint.ModelHash, text.Text);

        var suggestions = Assert.Single(reply.Blocks.OfType<SuggestionsBlock>());
        // Semantically adjacent real areas rank first (§3.5)…
        Assert.Equal("CustomerGroup", suggestions.Items[0].Label);
        // …and the escalation chip routes to NewModuleQuery.
        var escalation = suggestions.Items.Last();
        Assert.Contains("create a new module", escalation.Query, StringComparison.OrdinalIgnoreCase);

        var escalated = NewService().Query(escalation.Query);
        Assert.Equal("NewModuleQuery", escalated.Intent);
        Assert.True(escalated.Hit);
        var checklist = Assert.Single(escalated.Blocks.OfType<ChecklistBlock>());
        Assert.Contains(checklist.Steps, s => s.FilePath == "Store.Domain/Department.cs" && !s.Verified);
        Assert.Contains(checklist.Steps, s => s.Warnings.Any(w => w.RuleId == "HR-10"));
    }

    // ------------------------------------------------------------------- self-discovery (O3 / TEST-2)

    private sealed class Department
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SyntheticDbContext : DbContext
    {
        public SyntheticDbContext(DbContextOptions<SyntheticDbContext> options) : base(options) { }
        public DbSet<Department> Departments => Set<Department>();
    }

    [Fact]
    public void SyntheticEntity_BecomesQueryableWithZeroAssistantChanges()
    {
        var options = new DbContextOptionsBuilder<SyntheticDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        using var db = new SyntheticDbContext(options);

        ApiEndpointDescriptor[] endpoints =
        [
            new("AdminDepartments", "List", "GET", "api/admin/departments", "area:settings", false, false, null, null),
            new("AdminDepartments", "Create", "POST", "api/admin/departments", "area:settings", false, false, null, null)
        ];
        var snapshot = SnapshotBuilder.Build(db.Model, endpoints, "1.0.0-test", FixedNow, false, []);
        var service = new DevAssistantService(new FixedMetadataProvider(snapshot));

        // The exact query that was an honest miss in the shipped model now returns a populated matrix.
        var reply = service.Query("Show me all routes for departments.");

        Assert.Equal("RouteQuery", reply.Intent);
        Assert.Equal("Department", reply.Subject);
        Assert.True(reply.Hit);
        var matrix = Assert.Single(reply.Blocks.OfType<EndpointMatrixBlock>());
        Assert.Equal(2, matrix.Rows.Count);
        Assert.Contains(matrix.Rows, r => r.Verb == "POST" && r.Audited);
    }

    // -------------------------------------------------------------------------- determinism (TEST-1)

    [Fact]
    public void SameQuery_YieldsByteIdenticalReplies_AcrossFreshPipelines()
    {
        var first = JsonSerializer.Serialize(NewService().Query("Show the columns of Category"));
        var second = JsonSerializer.Serialize(NewService().Query("Show the columns of Category"));
        Assert.Equal(first, second);
    }

    [Fact]
    public void ModelHash_IsStableAcrossRebuilds()
    {
        Assert.Equal(BuildSnapshot().Fingerprint.ModelHash, BuildSnapshot().Fingerprint.ModelHash);
    }

    [Theory]
    [InlineData("Show the columns of catagories", "SchemaQuery", "Category")] // typo → bounded fuzzy tier
    [InlineData("What does the Order table look like?", "SchemaQuery", "Order")]
    [InlineData("foreign keys of Product", "RelationQuery", "Product")]
    [InlineData("Where is the code for Brand?", "LocateQuery", "Brand")]
    [InlineData("How does the audit trail work?", "ConceptExplain", "audit")]
    public void GoldenResolutions(string query, string intent, string subject)
    {
        var reply = NewService().Query(query);
        Assert.Equal(intent, reply.Intent);
        Assert.Equal(subject, reply.Subject);
        Assert.True(reply.Hit);
    }

    [Fact]
    public void UnknownIntent_ReturnsCapabilityCatalog_NeverGuesses()
    {
        var reply = NewService().Query("What is the weather in Amman?");
        Assert.Equal("Unknown", reply.Intent);
        Assert.False(reply.Hit);
        Assert.Single(reply.Blocks.OfType<SuggestionsBlock>());
    }

    [Fact]
    public void RuleQuery_ReturnsAllTenInvariants()
    {
        var reply = NewService().Query("What are the hard rules?");
        Assert.Equal("RuleQuery", reply.Intent);
        Assert.Equal(10, reply.Blocks.OfType<CalloutBlock>().Count(c => c.RuleId is not null));
    }

    // ------------------------------------------------------------------ follow-up context (spec §3.6)

    [Fact]
    public void SubjectlessFollowUp_CarriesTheMostRecentCompatibleSubject()
    {
        var reply = NewService().Query("and its routes?", contextSubjects: ["Category"]);

        Assert.Equal("RouteQuery", reply.Intent);
        Assert.Equal("Category", reply.Subject);
        Assert.True(reply.SubjectCarriedOver);
        Assert.Single(reply.Blocks.OfType<EndpointMatrixBlock>());
    }

    [Fact]
    public void ExplicitSubject_IgnoresContext()
    {
        var reply = NewService().Query("Show the columns of Brand", contextSubjects: ["Category"]);
        Assert.Equal("Brand", reply.Subject);
        Assert.False(reply.SubjectCarriedOver);
    }

    // ------------------------------------------------------------- exposure boundaries (SEC-10 / TEST-3)

    [Fact]
    public void SensitiveProperties_AppearByNameOnly()
    {
        var reply = NewService().Query("Show the columns of User");

        var grid = Assert.Single(reply.Blocks.OfType<PropertyGridBlock>());
        var passwordHash = grid.Rows.Single(r => r.Name == "PasswordHash");

        Assert.True(passwordHash.IsSensitive);
        Assert.Null(passwordHash.ClrType);
        Assert.Null(passwordHash.SqlType);
        Assert.Null(passwordHash.MaxLength);
        Assert.Null(passwordHash.DefaultValue);

        Assert.Contains(reply.Blocks.OfType<CalloutBlock>(),
            c => c.Text.Contains("sensitive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SnapshotEntities_SuppressDetailsForEverySensitiveColumn()
    {
        foreach (var property in Snapshot.Value.Entities.SelectMany(e => e.Properties).Where(p => p.IsSensitive))
        {
            Assert.Null(property.SqlType);
            Assert.Null(property.MaxLength);
            Assert.Null(property.DefaultValue);
        }
    }

    // ----------------------------------------------------------------------------- route matrix facts

    [Fact]
    public void RouteQuery_ReportsPolicyAndAuditParticipation()
    {
        var reply = NewService().Query("List the endpoints for categories");

        var matrix = Assert.Single(reply.Blocks.OfType<EndpointMatrixBlock>());
        Assert.Equal(3, matrix.Rows.Count);
        Assert.All(matrix.Rows, r => Assert.Equal("area:catalog", r.Policy));
        Assert.False(matrix.Rows.Single(r => r.Verb == "GET").Audited);
        Assert.True(matrix.Rows.Single(r => r.Verb == "POST").Audited);
    }

    [Fact]
    public void Capabilities_ListEveryIntentWithExamples()
    {
        var capabilities = NewService().Capabilities();
        Assert.Equal(IntentEngine.Intents.Count, capabilities.Capabilities.Count);
        Assert.All(capabilities.Capabilities, c => Assert.NotEmpty(c.Examples));
        Assert.Equal("1.0.0-test", capabilities.Fingerprint.AssemblyVersion);
    }

    // ------------------------------------------------------------------------- Arabic portal support

    [Theory]
    [InlineData("اعرض أعمدة المنتج", "SchemaQuery", "Product")]
    [InlineData("كيف أضيف خاصية جديدة إلى الفئات؟", "ChangePathQuery", "Category")]
    [InlineData("اعرض مسارات الطلبات", "RouteQuery", "Order")]
    [InlineData("ما الذي يشير إلى المنتج؟", "RelationQuery", "Product")]
    [InlineData("اشرح طبقة الترجمة", "ConceptExplain", "overlay")]
    [InlineData("أين كود العلامات التجارية؟", "LocateQuery", "Brand")]
    public void ArabicQueries_ResolveDeterministically(string query, string intent, string subject)
    {
        var reply = NewService().Query(query, culture: "ar");
        Assert.Equal(intent, reply.Intent);
        Assert.Equal(subject, reply.Subject);
        Assert.True(reply.Hit);
    }

    [Fact]
    public void ArabicCulture_ComposesArabicText_KeepingIdentifiersInEnglish()
    {
        var reply = NewService().Query("اعرض أعمدة المنتج", culture: "ar");

        var text = reply.Blocks.OfType<TextBlock>().First();
        Assert.Contains("أعمدة", text.Text);
        Assert.Contains("Product", text.Text);

        var english = NewService().Query("Show the columns of Product", culture: "en");
        Assert.Contains("Columns of", english.Blocks.OfType<TextBlock>().First().Text);
    }

    [Fact]
    public void ArabicRuleQuery_ReturnsArabicHardRules()
    {
        var reply = NewService().Query("ما هي القواعد الصارمة؟", culture: "ar");

        Assert.Equal("RuleQuery", reply.Intent);
        var callouts = reply.Blocks.OfType<CalloutBlock>().Where(c => c.RuleId is not null).ToList();
        Assert.Equal(10, callouts.Count);
        Assert.Contains("ExecuteUpdateAsync", callouts[0].Text); // identifiers stay in English
        Assert.Contains("التدقيق", callouts[0].Text);            // prose is Arabic
    }

    [Fact]
    public void ArabicCapabilities_UseArabicDescriptionsAndExamples()
    {
        var capabilities = NewService().Capabilities("ar");
        Assert.All(capabilities.Capabilities, c => Assert.NotEmpty(c.Examples));
        Assert.Contains(capabilities.Capabilities, c => c.Description.Contains("القواعد"));

        // Every Arabic example chip must itself resolve — chips are tappable queries.
        foreach (var capability in capabilities.Capabilities)
        {
            var reply = NewService().Query(capability.Examples[0], culture: "ar");
            Assert.NotEqual("Unknown", reply.Intent);
            Assert.NotEqual("Ambiguous", reply.Intent);
        }
    }

    [Fact]
    public void ArabicDepartmentsQuery_IsAnHonestMissWithAdjacentSubjects()
    {
        var reply = NewService().Query("اعرض مسارات الأقسام", culture: "ar");

        Assert.Equal("RouteQuery", reply.Intent);
        Assert.False(reply.Hit);
        var suggestions = Assert.Single(reply.Blocks.OfType<SuggestionsBlock>());
        Assert.Equal("CustomerGroup", suggestions.Items[0].Label);
        Assert.Contains("أنشئ", suggestions.Items.Last().Query);
    }

    [Fact]
    public void DefaultCulture_IsEnglish_SoExistingBehaviorIsUnchanged()
    {
        var reply = NewService().Query("Show the columns of Category");
        Assert.StartsWith("Columns of", reply.Blocks.OfType<TextBlock>().First().Text);
    }
}
