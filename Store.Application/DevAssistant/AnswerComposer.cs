namespace Store.Application.DevAssistant;

/// <summary>
/// Assembles structured replies (spec §2.2 step 5). No free-form prose is generated — only
/// parameterized template sentences over snapshot facts and curated knowledge-base content,
/// authored in both portal languages and selected by the request culture ("en" | "ar").
/// Technical identifiers (paths, commands, type names) stay in English in both languages.
/// </summary>
public sealed class AnswerComposer
{
    private readonly SystemMetadataSnapshot _snapshot;
    private readonly IntentEngine _engine;

    public AnswerComposer(SystemMetadataSnapshot snapshot, IntentEngine engine)
    {
        _snapshot = snapshot;
        _engine = engine;
    }

    public AssistantReply Compose(string queryText, QueryResolution resolution, string culture = "en")
    {
        var ar = culture == "ar";

        if (resolution.AmbiguousIntents.Count > 0)
            return Reply("Ambiguous", null, false, false, AmbiguousIntent(ar, resolution.AmbiguousIntents));

        if (resolution.Intent is null)
            return Reply("Unknown", null, false, false, UnknownIntent(ar));

        var intent = resolution.Intent;

        if (resolution.AmbiguousSubjects.Count > 0)
            return Reply(intent.Name, null, false, false, AmbiguousSubject(ar, queryText, resolution.AmbiguousSubjects));

        var subject = resolution.Subject;
        var subjectFits = subject is not null && subject.Fits(intent.AcceptedKinds);

        // A required subject that is missing or of the wrong kind degrades to the honest miss flow
        // (§3.2.4) — except NewModuleQuery, where an unknown name is the expected case.
        if (intent.RequiresSubject && !subjectFits && intent.Name != "NewModuleQuery")
            return Reply(intent.Name, null, false, false, Miss(ar, intent, resolution.UnresolvedTokens));

        var carryNote = resolution.SubjectCarriedOver && subject is not null
            ? L(ar,
                $" — **{subject.DisplayName}** carried over from your previous question",
                $" — **{subject.DisplayName}** منقول من سؤالك السابق")
            : string.Empty;

        var blocks = intent.Name switch
        {
            "ChangePathQuery" => ChangePath(ar, subject!.Entity!, carryNote),
            "NewModuleQuery" => NewModule(ar, subject, resolution.UnresolvedTokens),
            "SchemaQuery" => Schema(ar, subject!.Entity!, carryNote),
            "RouteQuery" => Routes(ar, subject!, carryNote),
            "RelationQuery" => Relations(ar, subject!.Entity!, carryNote),
            "LocateQuery" => Locate(ar, subject!),
            "ConceptExplain" => Concept(ar, subject!.TopicKey!),
            "RuleQuery" => Rules(ar),
            "CapabilityQuery" => Capabilities(ar),
            _ => UnknownIntent(ar)
        };

        return Reply(intent.Name, subject?.DisplayName, true, resolution.SubjectCarriedOver, blocks);
    }

    public CapabilitiesReply CapabilityCatalog(string culture = "en") =>
        new(_snapshot.Fingerprint,
            IntentEngine.Intents
                .Select(i => new CapabilityDto(i.Name, i.DescriptionFor(culture), i.ExamplesFor(culture)))
                .ToList(),
            _snapshot.Notices);

    private static string L(bool ar, string en, string arText) => ar ? arText : en;

    private AssistantReply Reply(string intent, string? subject, bool hit, bool carried, IReadOnlyList<AnswerBlock> blocks) =>
        new(intent, subject, hit, carried, _snapshot.Fingerprint, blocks);

    // ------------------------------------------------------------------------------- answer domains

    private IReadOnlyList<AnswerBlock> Schema(bool ar, EntitySnapshot entity, string carryNote)
    {
        var blocks = new List<AnswerBlock>
        {
            new TextBlock(
                L(ar, $"Schema of {entity.ClrName}", $"مخطط {entity.ClrName}"),
                L(ar,
                    $"Columns of **{entity.ClrName}** (table `{entity.TableName}`){carryNote}, read from the EF model of the running build.",
                    $"أعمدة **{entity.ClrName}** (الجدول `{entity.TableName}`){carryNote}، مقروءة من نموذج EF للنسخة العاملة.")),
            Grid(ar, entity, entity.Properties.Select(Row).ToList(), [])
        };

        AppendSchemaCaveats(ar, entity, blocks);
        return blocks;
    }

    private IReadOnlyList<AnswerBlock> Relations(bool ar, EntitySnapshot entity, string carryNote)
    {
        var outgoing = entity.Navigations
            .Select(n => new RelationRow("outgoing", n.Name, n.TargetEntity, n.Cardinality, n.DeleteBehavior));

        var incoming = _snapshot.Entities
            .Where(other => other.ClrName != entity.ClrName)
            .SelectMany(other => other.Navigations
                .Where(n => n.TargetEntity == entity.ClrName)
                .Select(n => new RelationRow("incoming", $"{other.ClrName}.{n.Name}", other.ClrName, n.Cardinality, n.DeleteBehavior)));

        var fkRows = entity.Properties.Where(p => p.IsForeignKey).Select(Row).ToList();

        return
        [
            new TextBlock(
                L(ar, $"Relationships of {entity.ClrName}", $"علاقات {entity.ClrName}"),
                L(ar,
                    $"Foreign keys and navigations of **{entity.ClrName}**{carryNote}, both directions.",
                    $"المفاتيح الأجنبية وعلاقات التنقل لكيان **{entity.ClrName}**{carryNote}، في الاتجاهين.")),
            Grid(ar, entity, fkRows,
                outgoing.Concat(incoming).OrderBy(r => r.Direction).ThenBy(r => r.Name, StringComparer.Ordinal).ToList())
        ];
    }

    private IReadOnlyList<AnswerBlock> Routes(bool ar, SubjectMatch subject, string carryNote)
    {
        var segment = subject.AreaSegment
            ?? (subject.Entity is { HasAdminController: true } e ? e.AdminAreaSegment : null);

        var rows = segment is null
            ? []
            : _snapshot.Endpoints
                .Where(ep => ep.Route.StartsWith($"api/admin/{segment}", StringComparison.OrdinalIgnoreCase)
                             || ep.Route.StartsWith($"api/{segment}", StringComparison.OrdinalIgnoreCase))
                .Select(ep => new EndpointRow(ep.Verb, ep.Route, ep.Action, ep.AllowAnonymous ? null : ep.Policy, IsAudited(ep)))
                .OrderBy(r => r.Route, StringComparer.Ordinal)
                .ThenBy(r => r.Verb, StringComparer.Ordinal)
                .ToList();

        if (rows.Count == 0)
        {
            var name = subject.DisplayName;
            return
            [
                new TextBlock(
                    L(ar, $"No routes for {name}", $"لا مسارات لـ {name}"),
                    L(ar,
                        $"**{name}** exists in the model but exposes no API routes in this build (fingerprint {_snapshot.Fingerprint.ModelHash}).",
                        $"**{name}** موجود في النموذج لكنه لا يكشف أي مسارات API في هذه النسخة (البصمة {_snapshot.Fingerprint.ModelHash}).")),
                new SuggestionsBlock(L(ar, "Try instead", "جرّب بدلًا من ذلك"), [Escalation(ar, name)])
            ];
        }

        return
        [
            new TextBlock(
                L(ar, $"Routes for {subject.DisplayName}", $"مسارات {subject.DisplayName}"),
                L(ar,
                    $"All **{rows.Count}** route(s) for **{subject.DisplayName}**{carryNote}, reflected from the deployed assembly.",
                    $"كل المسارات (**{rows.Count}**) الخاصة بـ **{subject.DisplayName}**{carryNote}، مستخرجة انعكاسيًا من الملف المنشور.")),
            new EndpointMatrixBlock(L(ar, $"{rows.Count} routes", $"{rows.Count} مسارًا"), segment!, rows)
        ];
    }

    private IReadOnlyList<AnswerBlock> ChangePath(bool ar, EntitySnapshot entity, string carryNote)
    {
        var artifacts = Conventions.ArtifactsFor(entity);
        var steps = new List<ChecklistStep>
        {
            Step(artifacts[0],
                L(ar, "Add the property to the entity class.", "أضف الخاصية إلى صنف الكيان."),
                warnings: [Warn(ar, "HR-6")]),
            Step(artifacts[1],
                L(ar, "Map the new column: type, length, nullability, index if queried.",
                      "خطّط العمود الجديد: النوع والطول وقابلية الفراغ، وفهرس إن كان يُستعلم عنه.")),
            new("Migration", null, true,
                L(ar,
                    "Generate the migration, inspect the Up()/Down() it produced — never apply blindly — then update the database.",
                    "ولّد الترحيل وافحص Up()/Down() الناتجتين — لا تطبّق أبدًا دون فحص — ثم حدّث قاعدة البيانات."),
                KnowledgeBase.MigrationsCommand + "\n" + KnowledgeBase.MigrationsApplyCommand,
                [Warn(ar, "HR-1")]),
            Step(artifacts[3],
                L(ar, $"Extend the DTO records (list/detail/upsert as needed) for {entity.ClrName}.",
                      $"وسّع سجلات DTO (قائمة/تفصيل/حفظ حسب الحاجة) الخاصة بـ {entity.ClrName}.")),
            Step(artifacts[2],
                L(ar, "Extend the controller's projection and Apply method to carry the new field.",
                      "وسّع إسقاط المتحكم ودالة Apply ليحملا الحقل الجديد."),
                warnings: entity.IsBilingual ? [Warn(ar, "HR-2")] : [])
        };

        if (entity.IsBilingual)
        {
            steps.Add(new("API", "Store.Application/Localization/ILocalizationService.cs", true,
                L(ar,
                    $"Customer-facing text on {entity.ClrName} must be bilingual: add the `<Field>En` pair to the upsert request, add a LocalizedProperty constant, include the pair in the controller's WriteEnglishAsync property map, and read it in the detail DTO via overlay.Get(...).",
                    $"النص الموجّه للعملاء في {entity.ClrName} يجب أن يكون ثنائي اللغة: أضف زوج `<Field>En` إلى طلب الحفظ، وأضف ثابت LocalizedProperty، وأدرج الزوج في خريطة WriteEnglishAsync بالمتحكم، واقرأه في DTO التفصيل عبر overlay.Get(...)."),
                null, [Warn(ar, "HR-2"), Warn(ar, "HR-9")]));
        }

        steps.Add(Step(artifacts[4],
            L(ar, "Mirror the DTO change in the TypeScript models — same commit as the backend change.",
                  "اعكس تغيير الـ DTO في نماذج TypeScript — في نفس commit تغيير الخلفية."),
            command: KnowledgeBase.BuildLibsCommand, warnings: [Warn(ar, "HR-5"), Warn(ar, "HR-8")]));
        steps.Add(Step(artifacts[6],
            entity.IsBilingual
                ? L(ar, "Surface the field in the admin form — use the shared multi-lang-input component for the Arabic/English pair.",
                        "أظهر الحقل في نموذج لوحة الإدارة — استخدم مكوّن multi-lang-input المشترك لزوج العربية/الإنجليزية.")
                : L(ar, "Surface the field in the admin form (and the storefront if customer-facing).",
                        "أظهر الحقل في نموذج لوحة الإدارة (وفي المتجر إن كان موجّهًا للعملاء).")));
        steps.Add(new("API", null, true,
            L(ar, "Build and run the backend test suite.", "ابنِ وشغّل حزمة اختبارات الخلفية."),
            "dotnet build\ndotnet test", []));

        return
        [
            new TextBlock(
                L(ar, $"Change path for {entity.ClrName}", $"مسار التعديل لـ {entity.ClrName}"),
                L(ar,
                    $"Adding a field to **{entity.ClrName}**{carryNote} touches every layer in this order."
                    + (entity.IsBilingual ? $" {entity.ClrName} participates in the bilingual overlay, so the English-pair step is included." : ""),
                    $"إضافة حقل إلى **{entity.ClrName}**{carryNote} تلمس كل الطبقات بهذا الترتيب."
                    + (entity.IsBilingual ? $" الكيان {entity.ClrName} مشارك في طبقة الترجمة، لذا أُدرجت خطوة الزوج الإنجليزي." : ""))),
            new ChecklistBlock(
                L(ar, $"Add a field to {entity.ClrName}", $"إضافة حقل إلى {entity.ClrName}"),
                L(ar, $"Add a field to {entity.ClrName}", $"إضافة حقل إلى {entity.ClrName}"),
                true, steps)
        ];
    }

    private IReadOnlyList<AnswerBlock> NewModule(bool ar, SubjectMatch? subject, IReadOnlyList<string> unresolvedTokens)
    {
        var blocks = new List<AnswerBlock>();

        if (subject?.Entity is not null)
        {
            blocks.Add(new TextBlock(
                L(ar, $"{subject.Entity.ClrName} already exists", $"{subject.Entity.ClrName} موجود بالفعل"),
                L(ar,
                    $"**{subject.Entity.ClrName}** already exists in this build — the steps below describe creating a module like it from scratch. To change the existing one instead, ask for its change path.",
                    $"**{subject.Entity.ClrName}** موجود بالفعل في هذه النسخة — الخطوات أدناه تصف إنشاء وحدة مثله من الصفر. لتعديل الموجود، اسأل عن مسار التعديل الخاص به.")));
        }

        var rawName = subject?.Entity?.ClrName
            ?? (unresolvedTokens.Count > 0 ? Capitalize(unresolvedTokens[0]) : "YourEntity");
        var plural = Conventions.Pluralize(rawName);
        var segment = Conventions.KebabPlural(rawName);

        var steps = new List<ChecklistStep>
        {
            new("Domain", $"Store.Domain/{rawName}.cs", false,
                L(ar,
                    "Create the entity class — plain C#, no EF Core here. Implement ISoftDeletable / ISeoEntity / IAuditedEntity where the module needs those disciplines.",
                    "أنشئ صنف الكيان — C# خالص بلا EF Core هنا. طبّق ISoftDeletable / ISeoEntity / IAuditedEntity حيث تحتاج الوحدة تلك الضوابط."),
                null, [Warn(ar, "HR-6")]),
            new("Data", $"Store.Data/Configurations/{rawName}Configuration.cs", false,
                L(ar,
                    "Create the IEntityTypeConfiguration<T>: table name, column types/lengths, indexes, relationships. It is picked up automatically by ApplyConfigurationsFromAssembly.",
                    "أنشئ IEntityTypeConfiguration<T>: اسم الجدول وأنواع/أطوال الأعمدة والفهارس والعلاقات. يُلتقط تلقائيًا عبر ApplyConfigurationsFromAssembly."),
                null, []),
            new("Migration", null, true,
                L(ar, "Generate, inspect and apply the migration.", "ولّد الترحيل وافحصه ثم طبّقه."),
                KnowledgeBase.MigrationsCommand + "\n" + KnowledgeBase.MigrationsApplyCommand, [Warn(ar, "HR-1")]),
            new("API", $"Store.Api/Controllers/Admin/Admin{plural}Controller.cs", false,
                L(ar,
                    $"Create the admin controller at /api/admin/{segment} with [Authorize(Policy = AuthPolicies.<Area>)], DTO records in Store.Api/Models/AdminModels.cs, a PagedResult list with .WithAuditStampsAsync(...), and overlay read/write if the content is bilingual. Copy AdminBrandsController as the exemplar.",
                    $"أنشئ متحكم الإدارة على /api/admin/{segment} مع [Authorize(Policy = AuthPolicies.<Area>)]، وسجلات DTO في Store.Api/Models/AdminModels.cs، وقائمة PagedResult مع .WithAuditStampsAsync(...)، وقراءة/كتابة طبقة الترجمة إن كان المحتوى ثنائي اللغة. انسخ AdminBrandsController نموذجًا."),
                null, [Warn(ar, "HR-2")]),
            new("API", "Store.Api/Infrastructure/AuthPolicies.cs", true,
                L(ar,
                    "If this is a new permission area: add the policy constant and its Area(...) registration in AddStorePolicies.",
                    "إن كانت هذه منطقة صلاحيات جديدة: أضف ثابت السياسة وتسجيلها Area(...) داخل AddStorePolicies."),
                null, [Warn(ar, "HR-10")]),
            new("Admin UI", "web/projects/admin/src/app/core/roles.ts", true,
                L(ar, "Mirror the same area in the AREA map — same commit as the policy.",
                      "اعكس المنطقة نفسها في خريطة AREA — في نفس commit السياسة."),
                null, [Warn(ar, "HR-10")]),
            new("data-access", "web/projects/data-access/src/lib/models.ts", true,
                L(ar, "Add the DTO interfaces mirroring the backend records.",
                      "أضف واجهات DTO المطابقة لسجلات الخلفية."),
                null, [Warn(ar, "HR-5")]),
            new("data-access", $"web/projects/data-access/src/lib/admin/admin-{segment}.service.ts", false,
                L(ar,
                    "Create the typed service: httpResource for reads, Observable commands for writes. Export it from public-api.ts.",
                    "أنشئ الخدمة المصنّفة: httpResource للقراءات وأوامر Observable للكتابات. وصدّرها من public-api.ts."),
                KnowledgeBase.BuildLibsCommand, [Warn(ar, "HR-8")]),
            new("Admin UI", $"web/projects/admin/src/app/features/{segment}/", false,
                L(ar,
                    "Create the lazy feature: list + form components, a route with roleGuard(...AREA.<area>), a sidebar entry in admin-layout, and translations in both assets/i18n files.",
                    "أنشئ الميزة الكسولة: مكوّنا قائمة ونموذج، ومسارًا مع roleGuard(...AREA.<area>)، وعنصر قائمة جانبية في admin-layout، وترجمات في ملفَي assets/i18n كليهما."),
                null, []),
            new("API", null, true,
                L(ar, "Build, test, and smoke the new area end-to-end.", "ابنِ واختبر وجرّب المنطقة الجديدة من طرف إلى طرف."),
                "dotnet build\ndotnet test", [])
        };

        blocks.Add(new TextBlock(
            L(ar, $"Scaffold a new module: {rawName}", $"بناء وحدة جديدة: {rawName}"),
            L(ar,
                $"Creating a new admin CRUD area for **{rawName}** follows the same vertical slice as every existing area. All file paths below are convention-derived (\"to create\") — verify each against the tree as you go.",
                $"إنشاء منطقة إدارة جديدة لـ **{rawName}** يتبع الشريحة الرأسية نفسها المتبعة في كل منطقة قائمة. كل المسارات أدناه مشتقة من الاصطلاح («للإنشاء») — تحقق من كلٍّ منها في الشجرة أثناء العمل.")));
        blocks.Add(new ChecklistBlock(
            L(ar, $"New module: {rawName}", $"وحدة جديدة: {rawName}"),
            L(ar, $"New admin area: {rawName}", $"منطقة إدارة جديدة: {rawName}"),
            true, steps));
        blocks.Add(new TextBlock(
            L(ar, "Self-discovery", "الاكتشاف الذاتي"),
            L(ar,
                $"Once the entity, configuration and controller ship, this assistant discovers **{rawName}** automatically on the next process start — no portal configuration needed.",
                $"بمجرد شحن الكيان والتهيئة والمتحكم، يكتشف هذا المساعد **{rawName}** تلقائيًا عند تشغيل العملية التالي — دون أي إعداد للبوابة.")));
        return blocks;
    }

    private IReadOnlyList<AnswerBlock> Locate(bool ar, SubjectMatch subject)
    {
        if (subject.Entity is not null)
        {
            var steps = Conventions.ArtifactsFor(subject.Entity)
                .Select(a => new ChecklistStep(a.Layer, a.Path, a.Verified, LocalizedArtifactDescription(ar, a), null, Array.Empty<StepWarning>()))
                .ToList();
            return
            [
                new TextBlock(
                    L(ar, $"Code for {subject.Entity.ClrName}", $"كود {subject.Entity.ClrName}"),
                    L(ar,
                        $"**{subject.Entity.ClrName}** is implemented across these layers. Backend paths marked verified were confirmed by reflection; frontend paths are convention-derived.",
                        $"**{subject.Entity.ClrName}** منفّذ عبر هذه الطبقات. مسارات الخلفية الموسومة «مؤكد» تحققنا منها انعكاسيًا؛ ومسارات الواجهة مشتقة من الاصطلاح.")),
                new ChecklistBlock(
                    L(ar, $"Files for {subject.Entity.ClrName}", $"ملفات {subject.Entity.ClrName}"),
                    L(ar, $"Where {subject.Entity.ClrName} lives", $"أين يعيش {subject.Entity.ClrName}"),
                    false, steps)
            ];
        }

        if (subject.TopicKey is not null)
            return Concept(ar, subject.TopicKey);

        var controllers = _snapshot.Endpoints
            .Where(ep => ep.Route.Contains($"/{subject.AreaSegment}", StringComparison.OrdinalIgnoreCase))
            .Select(ep => ep.Controller)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var areaSteps = controllers
            .Select(c => new ChecklistStep("API",
                $"Store.Api/Controllers/{(c.StartsWith("Admin", StringComparison.Ordinal) ? "Admin/" : "")}{c}Controller.cs",
                true,
                L(ar, $"The {c} controller.", $"المتحكم {c}."),
                null, Array.Empty<StepWarning>()))
            .ToList();
        areaSteps.Add(new ChecklistStep("Admin UI", $"web/projects/admin/src/app/features/{subject.AreaSegment}/",
            false,
            L(ar, "The admin feature folder (convention path — verify).", "مجلد الميزة في لوحة الإدارة (مسار اصطلاحي — تحقق منه)."),
            null, Array.Empty<StepWarning>()));

        return
        [
            new TextBlock(
                L(ar, $"Code for {subject.DisplayName}", $"كود {subject.DisplayName}"),
                L(ar, $"Files implementing the **{subject.DisplayName}** area.", $"الملفات المنفِّذة لمنطقة **{subject.DisplayName}**.")),
            new ChecklistBlock(
                L(ar, $"Files for {subject.DisplayName}", $"ملفات {subject.DisplayName}"),
                L(ar, $"Where {subject.DisplayName} lives", $"أين تعيش {subject.DisplayName}"),
                false, areaSteps)
        ];
    }

    private IReadOnlyList<AnswerBlock> Concept(bool ar, string topicKey)
    {
        var topic = KnowledgeBase.FindTopic(topicKey)!;
        var title = ar ? topic.TitleAr : topic.Title;
        var paragraphs = ar ? topic.ParagraphsAr : topic.Paragraphs;
        var blocks = new List<AnswerBlock>();
        blocks.AddRange(paragraphs.Select(p => new TextBlock(title, p)));
        blocks.AddRange(topic.RuleIds.Select(id => Callout(ar, KnowledgeBase.Rule(id))));
        blocks.Add(new CalloutBlock(
            L(ar, $"Read more: {topic.DocRef}", $"للمزيد: {topic.DocRef}"),
            "info", null,
            L(ar, $"The full write-up is in {topic.DocRef}.", $"الشرح الكامل في {topic.DocRef}."),
            topic.DocRef));
        return blocks;
    }

    private static IReadOnlyList<AnswerBlock> Rules(bool ar)
    {
        var blocks = new List<AnswerBlock>
        {
            new TextBlock(
                L(ar, "The hard rules", "القواعد الصارمة"),
                L(ar,
                    "These are invariants — breaking any of them causes silent data or security damage.",
                    "هذه ثوابت — كسر أيٍّ منها يسبب ضررًا صامتًا في البيانات أو الأمان."))
        };
        blocks.AddRange(KnowledgeBase.HardRules.Select(r => Callout(ar, r)));
        return blocks;
    }

    private IReadOnlyList<AnswerBlock> Capabilities(bool ar) =>
    [
        new TextBlock(
            L(ar, "What I can answer", "ما أستطيع الإجابة عنه"),
            L(ar,
                "I answer structural questions about this deployed build deterministically — from the EF model, the reflected API surface and a curated knowledge base. I never guess, never read data rows, and never write code.",
                "أجيب عن الأسئلة البنيوية حول هذه النسخة المنشورة بشكل حتمي — من نموذج EF وسطح الـ API المستخرج انعكاسيًا وقاعدة معرفة منسّقة. لا أخمّن أبدًا، ولا أقرأ صفوف البيانات، ولا أكتب كودًا.")),
        new SuggestionsBlock(
            L(ar, "Try one of these", "جرّب أحد هذه الأسئلة"),
            IntentEngine.Intents
                .Select(i => new SuggestionChip(i.DescriptionFor(ar ? "ar" : "en"), i.ExamplesFor(ar ? "ar" : "en")[0]))
                .ToList())
    ];

    // ------------------------------------------------------------------------ miss & ambiguity flows

    private IReadOnlyList<AnswerBlock> Miss(bool ar, IntentDefinition intent, IReadOnlyList<string> unresolvedTokens)
    {
        // Prefer a token that is not generic intent vocabulary — "show"/"اعرض" left over from a
        // losing intent must not shadow the actual unknown subject.
        var token = unresolvedTokens.FirstOrDefault(t => !IntentEngine.IsIntentVocabulary(t))
            ?? unresolvedTokens.FirstOrDefault();
        var chips = new List<SuggestionChip>();

        if (token is not null)
        {
            // Semantically adjacent real areas first, deterministically ranked (spec §3.5)...
            if (KnowledgeBase.AdjacentSubjects.TryGetValue(token, out var adjacent))
                chips.AddRange(adjacent.Select(a => new SuggestionChip(a, RephraseFor(ar, intent, a))));

            // ...then the nearest real subjects by string distance.
            chips.AddRange(_engine.NearestSubjects(token, 3)
                .Where(n => chips.All(c => !string.Equals(c.Label, n, StringComparison.OrdinalIgnoreCase)))
                .Select(n => new SuggestionChip(n, RephraseFor(ar, intent, n))));

            chips.Add(Escalation(ar, token));
        }
        else
        {
            chips.AddRange(IntentEngine.Intents.Take(4)
                .Select(i => new SuggestionChip(i.Name, i.ExamplesFor(ar ? "ar" : "en")[0])));
        }

        var what = token is null
            ? L(ar,
                "That question needs a subject (an entity, an API area, or a concept), and I could not find one in it.",
                "هذا السؤال يحتاج موضوعًا (كيانًا أو منطقة API أو مفهومًا)، ولم أجد أيًّا منها فيه.")
            : L(ar,
                $"No entity, table or API area named \"{token}\" exists in the deployed build (fingerprint {_snapshot.Fingerprint.ModelHash}, assembly {_snapshot.Fingerprint.AssemblyVersion}).",
                $"لا يوجد كيان أو جدول أو منطقة API باسم «{token}» في النسخة المنشورة (البصمة {_snapshot.Fingerprint.ModelHash}، الإصدار {_snapshot.Fingerprint.AssemblyVersion}).");

        return
        [
            new TextBlock(L(ar, "Not found in this build", "غير موجود في هذه النسخة"), what),
            new SuggestionsBlock(L(ar, "Did you mean", "هل تقصد"), chips)
        ];
    }

    private static IReadOnlyList<AnswerBlock> UnknownIntent(bool ar) =>
    [
        new TextBlock(
            L(ar, "Not something I can answer", "ليس مما أستطيع الإجابة عنه"),
            L(ar,
                "That is not something I can answer — I only answer structural questions about this system, deterministically. Here is what I can do.",
                "هذا ليس مما أستطيع الإجابة عنه — أنا أجيب فقط عن الأسئلة البنيوية حول هذا النظام، وبشكل حتمي. إليك ما أستطيع فعله.")),
        new SuggestionsBlock(
            L(ar, "Capabilities", "القدرات"),
            IntentEngine.Intents
                .Select(i => new SuggestionChip(i.DescriptionFor(ar ? "ar" : "en"), i.ExamplesFor(ar ? "ar" : "en")[0]))
                .ToList())
    ];

    private static IReadOnlyList<AnswerBlock> AmbiguousIntent(bool ar, IReadOnlyList<IntentDefinition> candidates) =>
    [
        new TextBlock(
            L(ar, "Ambiguous question", "سؤال محتمل لأكثر من معنى"),
            L(ar,
                "I can read that question two ways, and I never guess between near-ties. Pick one:",
                "يمكن قراءة سؤالك بطريقتين، وأنا لا أخمّن بين احتمالين متقاربين أبدًا. اختر واحدًا:")),
        new SuggestionsBlock(
            L(ar, "Did you mean", "هل تقصد"),
            candidates
                .Select(c => new SuggestionChip(c.DescriptionFor(ar ? "ar" : "en"), c.ExamplesFor(ar ? "ar" : "en")[0]))
                .ToList())
    ];

    private static IReadOnlyList<AnswerBlock> AmbiguousSubject(bool ar, string queryText, IReadOnlyList<SubjectMatch> candidates) =>
    [
        new TextBlock(
            L(ar, "Ambiguous subject", "موضوع محتمل لأكثر من معنى"),
            L(ar,
                "More than one subject matches that equally well. Pick one:",
                "أكثر من موضوع يطابق سؤالك بالدرجة نفسها. اختر واحدًا:")),
        new SuggestionsBlock(
            L(ar, "Did you mean", "هل تقصد"),
            candidates
                .Select(c => new SuggestionChip(c.DisplayName, ReplaceSubject(ar, queryText, c.DisplayName)))
                .ToList())
    ];

    // ---------------------------------------------------------------------------------------- utils

    private void AppendSchemaCaveats(bool ar, EntitySnapshot entity, List<AnswerBlock> blocks)
    {
        if (entity.Properties.Any(p => p.IsSensitive))
        {
            blocks.Add(new CalloutBlock(
                L(ar, "Sensitive columns suppressed", "أعمدة حسّاسة محجوبة"),
                "info", null,
                L(ar,
                    "Columns tagged sensitive are shown by name only — their type, length and default are suppressed, and they are excluded from the audit trail by AuditSecrets.",
                    "الأعمدة الموسومة «حسّاسة» تُعرض بالاسم فقط — نوعها وطولها وقيمتها الافتراضية محجوبة، وهي مستبعدة من سجل التدقيق عبر AuditSecrets."),
                "TECHNICAL-DOCUMENTATION.md §9"));
        }

        if (_snapshot.HasPendingMigrations == true)
        {
            blocks.Add(new CalloutBlock(
                L(ar, "Pending migrations", "ترحيلات معلّقة"),
                "critical", null,
                L(ar,
                    "The code model and the database schema disagree: there are pending EF migrations. Apply them before trusting column answers.",
                    "نموذج الكود ومخطط قاعدة البيانات غير متطابقين: هناك ترحيلات EF معلّقة. طبّقها قبل الوثوق بإجابات الأعمدة."),
                "TECHNICAL-DOCUMENTATION.md §11"));
        }
    }

    private static PropertyGridBlock Grid(bool ar, EntitySnapshot entity, IReadOnlyList<PropertyGridRow> rows, IReadOnlyList<RelationRow> relations)
    {
        var markers = new List<string>();
        if (entity.IsSoftDeletable) markers.Add("ISoftDeletable");
        if (entity.IsSeoEntity) markers.Add("ISeoEntity");
        if (entity.IsAudited) markers.Add("IAuditedEntity");
        return new PropertyGridBlock(
            L(ar,
                $"{entity.ClrName} ({entity.TableName}), {rows.Count} column(s)",
                $"{entity.ClrName} ({entity.TableName})، {rows.Count} عمودًا"),
            entity.ClrName, entity.TableName, markers, entity.IsBilingual, rows, relations);
    }

    private static PropertyGridRow Row(PropertySnapshot p) => new(
        p.Name,
        p.IsSensitive ? null : p.ClrType,
        p.SqlType, p.MaxLength,
        p.IsSensitive ? null : p.IsNullable,
        p.DefaultValue,
        p.IsPrimaryKey, p.IsForeignKey, p.ForeignKeyPrincipal, p.IsIndexed, p.IsUnique, p.IsSensitive);

    private bool IsAudited(ApiEndpointDescriptor ep) =>
        ep.Route.StartsWith("api/admin", StringComparison.OrdinalIgnoreCase)
        && ep.Verb is "POST" or "PUT" or "PATCH" or "DELETE"
        && !ep.SkipAudit;

    private static ChecklistStep Step(CorrelatedArtifact artifact, string description,
        string? command = null, IReadOnlyList<StepWarning>? warnings = null) =>
        new(artifact.Layer, artifact.Path, artifact.Verified, description, command, warnings ?? Array.Empty<StepWarning>());

    private static string LocalizedArtifactDescription(bool ar, CorrelatedArtifact artifact)
    {
        if (!ar)
            return artifact.Description;
        return artifact.Layer switch
        {
            "Domain" => "صنف الكيان (C# خالص، بلا EF Core هنا).",
            "Data" => "تخطيط EF: اسم الجدول وأنواع/أطوال الأعمدة والفهارس والعلاقات.",
            "API" when artifact.Path.EndsWith("Controller.cs", StringComparison.Ordinal) =>
                "متحكم لوحة الإدارة وإسقاطات الـ DTO الخاصة به.",
            "API" => "سجلات DTO (قائمة/تفصيل/حفظ) — العقد العام.",
            "data-access" when artifact.Path.EndsWith("models.ts", StringComparison.Ordinal) =>
                "مرآة TypeScript لسجلات DTO (في نفس commit تغيير الخلفية).",
            "data-access" => "خدمة Angular المصنّفة لمتحكم الإدارة.",
            "Admin UI" => "منطقة الميزة الكسولة في لوحة الإدارة (مكوّنات القائمة والنموذج، والمسار، وعنصر القائمة الجانبية).",
            _ => artifact.Description
        };
    }

    private static StepWarning Warn(bool ar, string ruleId)
    {
        var rule = KnowledgeBase.Rule(ruleId);
        return new StepWarning(rule.Severity, rule.Id, ar ? rule.TextAr : rule.Text, rule.DocRef);
    }

    private static CalloutBlock Callout(bool ar, HardRule rule) =>
        new(L(ar, $"{rule.Id}: hard rule", $"{rule.Id}: قاعدة صارمة"),
            rule.Severity, rule.Id, ar ? rule.TextAr : rule.Text, rule.DocRef);

    private static SuggestionChip Escalation(bool ar, string name) =>
        new(L(ar, $"Create a module called {name}", $"إنشاء وحدة باسم {name}"),
            L(ar, $"How do I create a new module called {name}?", $"كيف أنشئ وحدة جديدة باسم {name}؟"));

    private static string RephraseFor(bool ar, IntentDefinition intent, string subjectName) => intent.Name switch
    {
        "SchemaQuery" => L(ar, $"Show the columns of {subjectName}", $"اعرض أعمدة {subjectName}"),
        "RouteQuery" => L(ar, $"Show me all routes for {subjectName}", $"اعرض مسارات {subjectName}"),
        "RelationQuery" => L(ar, $"What references {subjectName}?", $"ما الذي يشير إلى {subjectName}؟"),
        "ChangePathQuery" => L(ar, $"How do I add a new property to {subjectName}?", $"كيف أضيف خاصية جديدة إلى {subjectName}؟"),
        "LocateQuery" => L(ar, $"Where is the code for {subjectName}?", $"أين كود {subjectName}؟"),
        _ => L(ar, $"Show the columns of {subjectName}", $"اعرض أعمدة {subjectName}")
    };

    private static string ReplaceSubject(bool ar, string queryText, string subjectName) =>
        L(ar,
            $"{queryText.TrimEnd('?', '؟', '.', ' ')} — specifically {subjectName}",
            $"{queryText.TrimEnd('?', '؟', '.', ' ')} — تحديدًا {subjectName}");

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
