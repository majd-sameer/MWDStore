namespace Store.Application.DevAssistant;

/// <summary>A curated concept topic (spec §2.3 Source D). Content is authored, versioned with the code, bilingual.</summary>
public sealed record KnowledgeTopic(
    string Key,
    string Title,
    string TitleAr,
    IReadOnlyList<string> Synonyms,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<string> ParagraphsAr,
    IReadOnlyList<string> RuleIds,
    string DocRef);

/// <summary>One of the codebase's hard rules (handoff doc §18), in both portal languages.</summary>
public sealed record HardRule(string Id, string Severity, string Text, string TextAr, string DocRef);

/// <summary>
/// The hand-authored knowledge layer: facts that cannot be derived from metadata — the hard rules,
/// concept explanations, operational facts and the subject synonym dictionary. A strongly-typed
/// static registry, unit-testable, versioned with the code. Technical identifiers (paths, commands,
/// type names) stay in English inside the Arabic text, as is standard in Arabic developer writing.
/// </summary>
public static class KnowledgeBase
{
    public const string MigrationsCommand =
        "dotnet ef migrations add <DescriptiveName> --project Store.Data --startup-project Store.Api";

    public const string MigrationsApplyCommand =
        "dotnet ef database update --project Store.Data --startup-project Store.Api";

    public const string BuildLibsCommand = "npm run build:libs";

    public static readonly IReadOnlyList<HardRule> HardRules =
    [
        new("HR-1", "critical",
            "Never use ExecuteUpdateAsync / ExecuteDeleteAsync. The audit trail is captured in StoreDbContext.SaveChanges; bulk operators bypass it. Load, modify, save.",
            "لا تستخدم ExecuteUpdateAsync / ExecuteDeleteAsync أبدًا. سجلّ التدقيق يُلتقط داخل StoreDbContext.SaveChanges، والعمليات الجماعية تتجاوزه. حمِّل الكيان، عدِّله، ثم احفظ.",
            "TECHNICAL-DOCUMENTATION.md §18.1"),
        new("HR-2", "critical",
            "Never make LocalizedContentWriter save. It stages overlay rows; the calling controller owns the single transactional SaveChangesAsync.",
            "لا تجعل LocalizedContentWriter يحفظ أبدًا. هو يجهّز صفوف الترجمة فقط؛ والمتحكم المستدعي هو مالك SaveChangesAsync الواحدة ضمن معاملة واحدة.",
            "TECHNICAL-DOCUMENTATION.md §18.2"),
        new("HR-3", "critical",
            "Don't merge SaveChangesAsync pairs where the second save consumes a generated ID from the first (order → coupon usage; entity → overlay in admin creates).",
            "لا تدمج استدعاءَي SaveChangesAsync عندما يعتمد الحفظ الثاني على معرّف ولّده الأول (الطلب → استخدام الكوبون؛ الكيان → صفوف الترجمة عند الإنشاء في لوحة الإدارة).",
            "TECHNICAL-DOCUMENTATION.md §18.3"),
        new("HR-4", "warning",
            "Don't add EF global query filters casually. Soft-delete scoping is explicit per query; a global filter changes every query's SQL and breaks the admin includeDeleted toggles.",
            "لا تضف EF global query filters باستسهال. نطاق الحذف الناعم صريح في كل استعلام؛ والفلتر العام يغيّر SQL كلَّ استعلام ويكسر مفاتيح includeDeleted في لوحة الإدارة.",
            "TECHNICAL-DOCUMENTATION.md §18.4"),
        new("HR-5", "critical",
            "DTO records, routes and status codes are a public contract with data-access/models.ts. Change both sides in one commit; never repurpose an existing field.",
            "سجلّات DTO والمسارات ورموز الحالة عقدٌ عام مع data-access/models.ts. عدِّل الطرفين في commit واحد، ولا تُعِد استخدام حقل قائم لغرض آخر أبدًا.",
            "TECHNICAL-DOCUMENTATION.md §18.5"),
        new("HR-6", "warning",
            "Keep Store.Domain free of EF Core, and keep entities out of controller responses.",
            "أبقِ Store.Domain خاليًا من EF Core، وأبقِ الكيانات خارج استجابات المتحكمات.",
            "TECHNICAL-DOCUMENTATION.md §18.6"),
        new("HR-7", "critical",
            "Access token stays in memory; refresh token stays httpOnly. Do not persist tokens to web storage; do not move the API cross-origin without redesigning the cookie flow.",
            "رمز الوصول يبقى في الذاكرة، ورمز التحديث يبقى في كوكي httpOnly. لا تخزّن الرموز في web storage، ولا تنقل الـ API إلى أصلٍ مختلف دون إعادة تصميم تدفق الكوكيز.",
            "TECHNICAL-DOCUMENTATION.md §18.7"),
        new("HR-8", "warning",
            "Frontend library changes require `npm run build:libs` before app builds pick them up (tsconfig maps the libs to dist/).",
            "أي تعديل على مكتبات الواجهة يتطلب `npm run build:libs` قبل أن تلتقطه بنيات التطبيقات (tsconfig يوجّه المكتبات إلى dist/).",
            "TECHNICAL-DOCUMENTATION.md §18.8"),
        new("HR-9", "critical",
            "Moderation status values (1/5/8) and the `ProperyName` column spelling are load-bearing legacy — they match migrated SimplCommerce data. Don't renumber or rename.",
            "قيم حالة المراجعة (1/5/8) وتهجئة العمود `ProperyName` إرثٌ وظيفي مقصود — فهي تطابق بيانات SimplCommerce المرحَّلة. لا تعد ترقيمها ولا تعد تسميتها.",
            "TECHNICAL-DOCUMENTATION.md §18.9"),
        new("HR-10", "critical",
            "AuthPolicies (API) and the AREA map (admin roles.ts) must stay in sync — they are two halves of one permission model.",
            "يجب أن يبقى AuthPolicies (في الـ API) وخريطة AREA (في roles.ts بلوحة الإدارة) متزامنين — فهما نصفا نموذج صلاحيات واحد.",
            "TECHNICAL-DOCUMENTATION.md §18.10")
    ];

    public static HardRule Rule(string id) => HardRules.First(r => r.Id == id);

    public static readonly IReadOnlyList<KnowledgeTopic> Topics =
    [
        new("overlay", "The bilingual content overlay", "طبقة المحتوى ثنائية اللغة",
            ["overlay", "localization", "bilingual", "translation", "translations", "arabic", "english", "culture", "language",
             "ترجمه", "تعريب", "لغه", "ثنائيه"],
            [
                "Base entity columns (Product.Name, Category.Description, Page.Body, …) hold Arabic — the site's default culture. English text lives in one overlay table, LocalizedContentProperty (EntityType, EntityId | EntityKey, CultureId, ProperyName, Value). The ProperyName misspelling is historical and load-bearing.",
                "Read path: controllers call RequestCulture.OverlayCultureId(Request), load base rows, then ILocalizationService.GetOverlayAsync(...) and re-project DTOs with overlay.Apply(id, property, baseValue).",
                "Write path: admin upserts carry paired fields (Name + NameEn). Controllers write the entity, then stage overlay changes with ILocalizedContentWriter.SetAsync/SetManyAsync — a blank value removes the override. The writer stages only; the controller saves once so entity and overlays commit in one transaction.",
                "Do not confuse content overlays with UI translations: static UI strings come from @ngx-translate JSON bundles in each app's assets/i18n."
            ],
            [
                "أعمدة الكيان الأساسية (Product.Name وCategory.Description وPage.Body …) تحمل العربية — الثقافة الافتراضية للموقع. النص الإنجليزي يعيش في جدول تراكب واحد هو LocalizedContentProperty بالأعمدة (EntityType, EntityId | EntityKey, CultureId, ProperyName, Value). تهجئة ProperyName الخاطئة تاريخية ومقصود الإبقاء عليها.",
                "مسار القراءة: تستدعي المتحكمات RequestCulture.OverlayCultureId(Request)، ثم تحمّل الصفوف الأساسية، ثم ILocalizationService.GetOverlayAsync(...)، وتعيد إسقاط الـ DTO عبر overlay.Apply(id, property, baseValue).",
                "مسار الكتابة: طلبات الحفظ في لوحة الإدارة تحمل الحقول المزدوجة (Name + NameEn). يكتب المتحكم الكيان ثم يجهّز تغييرات الترجمة عبر ILocalizedContentWriter.SetAsync/SetManyAsync — والقيمة الفارغة تزيل التجاوز. الكاتب يجهّز فقط؛ والمتحكم يحفظ مرة واحدة ليُرتكب الكيان وصفوف الترجمة في معاملة واحدة.",
                "لا تخلط بين ترجمات المحتوى وترجمات الواجهة: نصوص الواجهة الثابتة تأتي من حزم @ngx-translate JSON في assets/i18n لكل تطبيق."
            ],
            ["HR-2", "HR-9"],
            "TECHNICAL-DOCUMENTATION.md §8"),
        new("audit", "The audit trail", "سجل التدقيق",
            ["audit", "auditing", "auditlog", "trail", "تدقيق", "سجل"],
            [
                "Change capture happens in the data layer: StoreDbContext.SaveChanges snapshots every Added/Modified/Deleted entry into a scoped IAuditContext buffer before saving — entity type, state, id, display name and old/new scalar values. Sensitive properties are excluded by AuditSecrets.",
                "Entry writing happens in the API layer: the global AuditActionFilter persists an AuditLog row for every successful 2xx admin write, combining the captured changes with the JWT actor, area, client IP and correlation id. [SkipAudit] opts an action out — used where an endpoint writes its own richer entry via IAuditService.",
                "Most entities have no CreatedBy columns; admin lists derive created-by/modified-by from the log via IAuditStampReader, overlaid with result.WithAuditStampsAsync(...)."
            ],
            [
                "التقاط التغييرات يحدث في طبقة البيانات: StoreDbContext.SaveChanges يلتقط كل إدخال Added/Modified/Deleted في مخزن IAuditContext قبل الحفظ — نوع الكيان وحالته ومعرّفه واسمه المعروض والقيم القديمة/الجديدة. الخصائص الحسّاسة تُستبعد عبر AuditSecrets.",
                "كتابة السجل تحدث في طبقة الـ API: فلتر AuditActionFilter العام يحفظ صفَّ AuditLog لكل عملية كتابة إدارية ناجحة (2xx)، جامعًا التغييرات الملتقطة مع هوية JWT والمنطقة وعنوان IP ومعرّف الترابط. سمة [SkipAudit] تُخرج الإجراء من الفلتر — وتُستخدم حيث يكتب المسار سجلَّه الأغنى بنفسه عبر IAuditService.",
                "معظم الكيانات بلا أعمدة CreatedBy؛ قوائم لوحة الإدارة تشتق «أنشأه/عدّله» من السجل عبر IAuditStampReader ثم result.WithAuditStampsAsync(...)."
            ],
            ["HR-1"],
            "TECHNICAL-DOCUMENTATION.md §9"),
        new("auth", "Authentication and authorization", "المصادقة والتخويل",
            ["auth", "authentication", "authorization", "jwt", "login", "refresh", "policy", "policies", "role", "roles",
             "permission", "permissions", "صلاحيات", "مصادقه", "دخول", "تخويل"],
            [
                "Access is a HMAC-SHA256 JWT issued by JwtTokenService, kept in memory only on the frontend. The refresh token is a 256-bit random value whose SHA-256 hash is stored on the user; the raw value travels exclusively in the httpOnly refresh_token cookie and rotates on every issue.",
                "Authorization is pure role-based policies (Store.Api/Infrastructure/AuthPolicies.cs, names `area:<name>`), registered by AddStorePolicies. super-admin and admin are in every operational policy.",
                "The admin app's AREA map in web/projects/admin/src/app/core/roles.ts mirrors the policy table for menus and route guards — frontend guards are UX, not security; the controller policy is the enforcement point."
            ],
            [
                "رمز الوصول هو JWT بتوقيع HMAC-SHA256 يصدره JwtTokenService، ويُحفظ في ذاكرة الواجهة فقط. رمز التحديث قيمة عشوائية 256-بت يُخزَّن تجزئتها SHA-256 لدى المستخدم؛ والقيمة الخام تنتقل حصرًا في كوكي refresh_token من نوع httpOnly وتتبدّل عند كل إصدار.",
                "التخويل سياسات أدوار خالصة (Store.Api/Infrastructure/AuthPolicies.cs بأسماء `area:<name>`) تُسجَّل عبر AddStorePolicies. الدوران super-admin وadmin عضوان في كل سياسة تشغيلية.",
                "خريطة AREA في web/projects/admin/src/app/core/roles.ts تعكس جدول السياسات للقوائم وحرّاس المسارات — حرّاس الواجهة تجربة استخدام لا أمانًا؛ نقطة الإنفاذ هي سياسة المتحكم."
            ],
            ["HR-7", "HR-10"],
            "TECHNICAL-DOCUMENTATION.md §7"),
        new("seeding", "Startup seeders", "بذر البيانات عند الإقلاع",
            ["seeding", "seeder", "seeders", "seed", "بذر", "تهيئه"],
            [
                "All seeders run from Program.cs on every boot, in order: IdentitySeeder (roles, guest account, bootstrap super-admin from the AdminUser section — silently skipped if AdminUser:Password is absent), LocationSeeder (Jordan), CatalogSeeder (catalog.seed.json), LocalizationSeeder, ContentBlockSeeder, NewsCategorySeeder. All are idempotent.",
                "The schema itself is NOT auto-migrated — apply EF migrations as a deploy step before booting against a fresh database."
            ],
            [
                "كل البذّارات تعمل من Program.cs عند كل إقلاع وبالترتيب: IdentitySeeder (الأدوار وحساب الضيف والمدير الأعلى من قسم AdminUser — ويُتخطى بصمت إن غاب AdminUser:Password)، ثم LocationSeeder (الأردن)، ثم CatalogSeeder (catalog.seed.json)، ثم LocalizationSeeder وContentBlockSeeder وNewsCategorySeeder. جميعها آمنة التكرار.",
                "المخطط نفسه لا يُرحَّل تلقائيًا — طبّق ترحيلات EF كخطوة نشر قبل الإقلاع على قاعدة بيانات جديدة."
            ],
            [],
            "TECHNICAL-DOCUMENTATION.md §12"),
        new("migrations", "EF Core migrations", "ترحيلات EF Core",
            ["migration", "migrations", "ef", "efcore", "database", "db", "ترحيل", "ترحيلات", "هجره"],
            [
                "Entity mapping lives in one IEntityTypeConfiguration<T> class per entity under Store.Data/Configurations/. Migrations live in Store.Data/Migrations/, generated with: " + MigrationsCommand,
                "Inspect the generated Up()/Down() — never apply blindly — then: " + MigrationsApplyCommand,
                "Refactor-safety trick: when refactoring mapping code that must not change the schema, add a scratch migration, require it to be empty, then remove it."
            ],
            [
                "تخطيط الكيانات يعيش في صنف IEntityTypeConfiguration<T> واحد لكل كيان تحت Store.Data/Configurations/. والترحيلات تعيش في Store.Data/Migrations/ وتولَّد بالأمر: " + MigrationsCommand,
                "افحص Up()/Down() المولّدتين — لا تطبّق أبدًا دون فحص — ثم نفّذ: " + MigrationsApplyCommand,
                "حيلة أمان عند إعادة الهيكلة: حين تعيد هيكلة كود التخطيط دون تغيير المخطط، أضف ترحيلًا مؤقتًا واشترط أن يكون فارغًا ثم أزله."
            ],
            ["HR-1", "HR-4"],
            "TECHNICAL-DOCUMENTATION.md §11"),
        new("deployment", "Deployment topology", "بنية النشر",
            ["deployment", "deploy", "iis", "production", "publish", "hosting", "نشر", "استضافه", "انتاج"],
            [
                "Production runs behind IIS with URL Rewrite + ARR: the storefront is a Node SSR service (server.mjs, port 4000), the admin SPA is served statically, and /api + /user-content reverse-proxy to the internal Kestrel API.",
                "Deploy order matters: database migrations → API → storefront → admin → wiring. user-content/ is not in the database — back it up separately and never wipe it on redeploy."
            ],
            [
                "الإنتاج يعمل خلف IIS مع URL Rewrite وARR: المتجر خدمة Node SSR (server.mjs على المنفذ 4000)، ولوحة الإدارة SPA تُقدَّم ملفاتها ثابتة، بينما /api و/user-content يمرّان بوكيل عكسي إلى Kestrel الداخلي.",
                "ترتيب النشر مهم: ترحيلات قاعدة البيانات → الـ API → المتجر → لوحة الإدارة → الربط. مجلد user-content/ ليس في قاعدة البيانات — انسخه احتياطيًا على حدة ولا تمسحه عند إعادة النشر أبدًا."
            ],
            [],
            "TECHNICAL-DOCUMENTATION.md §19"),
        new("media", "Media and uploads", "الوسائط والرفع",
            ["media", "upload", "uploads", "image", "images", "file", "files", "وسائط", "صور", "صوره", "رفع"],
            [
                "Uploads are admin-only via POST /api/admin/media (max 10 MB, extension allowlist). LocalMediaStorage writes to Store.Api/user-content/ as {GUID}{ext}; files are served publicly at /user-content/<filename> before authentication runs.",
                "Entities reference media by FK; DTO projections resolve filenames via IMediaUrlBuilder.GetUrl — never hand-build media URLs. There is no server-side resizing."
            ],
            [
                "الرفع مقصور على لوحة الإدارة عبر POST /api/admin/media (حد أقصى 10 م.ب، وقائمة امتدادات مسموحة). يكتب LocalMediaStorage إلى Store.Api/user-content/ بصيغة {GUID}{ext}؛ وتُقدَّم الملفات علنًا على /user-content/<filename> قبل تشغيل المصادقة.",
                "تشير الكيانات إلى الوسائط بمفاتيح أجنبية؛ وإسقاطات الـ DTO تحوّل أسماء الملفات إلى روابط عبر IMediaUrlBuilder.GetUrl — لا تبنِ روابط الوسائط يدويًا أبدًا. لا يوجد تحجيم للصور على الخادم."
            ],
            [],
            "TECHNICAL-DOCUMENTATION.md §10"),
        new("testing", "Testing", "الاختبارات",
            ["testing", "test", "tests", "xunit", "vitest", "اختبار", "اختبارات"],
            [
                "Backend: tests/Store.Application.Tests (xUnit + EF Core InMemory) exercises Application services; money-sensitive logic (order totals, tax, coupons, stock) must stay green. Run `dotnet test`, filter with --filter \"FullyQualifiedName~Name\".",
                "Frontend: Vitest via the Angular unit-test builder (`ng test`); specs are co-located *.spec.ts."
            ],
            [
                "الخلفية: مشروع tests/Store.Application.Tests (بـ xUnit وEF Core InMemory) يختبر خدمات طبقة Application؛ والمنطق المالي الحسّاس (مجاميع الطلبات والضريبة والكوبونات والمخزون) يجب أن يبقى ناجحًا. شغّل `dotnet test` وصفِّ بـ --filter \"FullyQualifiedName~Name\".",
                "الواجهة: Vitest عبر بانى اختبارات Angular ‏(`ng test`)؛ وملفات الاختبار *.spec.ts مجاورة لمصدرها."
            ],
            [],
            "TECHNICAL-DOCUMENTATION.md §16"),
        new("build", "Frontend build pipeline", "خط بناء الواجهة",
            ["build", "libs", "buildlibs", "compile", "بناء"],
            [
                "The workspace tsconfig maps the library imports (core, data-access, ui, util) to their built output in dist/, not to source — so libraries must be built (`npm run build:libs`) before either app can compile or serve, and rebuilt after any lib change.",
                "Install with `npm ci --legacy-peer-deps` (ng-bootstrap declares an older Angular peer). `npm run build` builds libs then apps; a prebuild hook lints."
            ],
            [
                "ملف tsconfig في مساحة العمل يوجّه استيراد المكتبات (core وdata-access وui وutil) إلى مخرجاتها المبنية في dist/ لا إلى المصدر — لذا يجب بناء المكتبات (`npm run build:libs`) قبل أن يُترجم أي تطبيق أو يعمل، وإعادة بنائها بعد أي تعديل عليها.",
                "ثبّت بـ `npm ci --legacy-peer-deps` (لأن ng-bootstrap يعلن نظير Angular أقدم). الأمر `npm run build` يبني المكتبات ثم التطبيقات؛ وخطاف prebuild يشغّل الفاحص."
            ],
            ["HR-8"],
            "TECHNICAL-DOCUMENTATION.md §4.2")
    ];

    public static KnowledgeTopic? FindTopic(string key) =>
        Topics.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Subject synonym dictionary (spec §3.3): alternative developer vocabulary → the real subject
    /// (entity CLR name, admin area segment, or topic key) it should resolve to. Arabic entries are
    /// listed in their folded form (see IntentEngine.FoldArabic) with the definite article stripped.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SubjectSynonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = "Product",
            ["sku"] = "Product",
            ["article"] = "NewsItem",
            ["post"] = "NewsItem",
            ["blog"] = "NewsItem",
            ["cms"] = "Page",
            ["client"] = "customers",
            ["shipment"] = "Shipment",
            ["shipping"] = "Shipment",
            ["stock"] = "inventory",
            ["warehouse"] = "Warehouse",
            ["coupon"] = "CartRule",
            ["promotion"] = "CartRule",
            ["discount"] = "CartRule",
            ["supplier"] = "Vendor",
            ["manufacturer"] = "Brand",
            ["picture"] = "media",
            ["photo"] = "media",
            ["i18n"] = "overlay",
            ["rtl"] = "overlay",
            ["permission"] = "auth",
            ["security"] = "auth",
            // Arabic developer vocabulary → the same real subjects.
            ["منتج"] = "Product",
            ["منتجات"] = "Product",
            ["فئه"] = "Category",
            ["فئات"] = "Category",
            ["تصنيف"] = "Category",
            ["تصنيفات"] = "Category",
            ["طلب"] = "Order",
            ["طلبات"] = "Order",
            ["علامه"] = "Brand",
            ["علامات"] = "Brand",
            ["ماركه"] = "Brand",
            ["عميل"] = "customers",
            ["عملاء"] = "customers",
            ["زبون"] = "customers",
            ["مستخدم"] = "User",
            ["مستخدمين"] = "User",
            ["مخزون"] = "inventory",
            ["مستودع"] = "Warehouse",
            ["مستودعات"] = "Warehouse",
            ["شحن"] = "Shipment",
            ["شحنه"] = "Shipment",
            ["كوبون"] = "CartRule",
            ["خصم"] = "CartRule",
            ["عروض"] = "CartRule",
            ["مورد"] = "Vendor",
            ["موردين"] = "Vendor",
            ["بائع"] = "Vendor",
            ["صفحه"] = "Page",
            ["صفحات"] = "Page",
            ["خبر"] = "NewsItem",
            ["اخبار"] = "NewsItem",
            ["سله"] = "CartItem",
            ["ضريبه"] = "tax",
            ["ضرائب"] = "tax",
            ["دفع"] = "payments",
            ["مدفوعات"] = "payments"
        };

    /// <summary>
    /// Semantically adjacent real subjects a newcomer might mean by a term that does not exist in
    /// this codebase — ranked first among miss suggestions (spec §3.5).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AdjacentSubjects =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["department"] = ["CustomerGroup", "Vendor", "Warehouse"],
            ["team"] = ["User", "CustomerGroup"],
            ["store"] = ["Warehouse", "Vendor"],
            ["branch"] = ["Warehouse", "StateOrProvince"],
            ["tag"] = ["Category", "ProductAttribute"],
            ["invoice"] = ["Order"],
            ["basket"] = ["CartItem", "Checkout"],
            // Arabic equivalents of the same near-miss vocabulary (folded, article-stripped).
            ["قسم"] = ["CustomerGroup", "Vendor", "Warehouse"],
            ["اقسام"] = ["CustomerGroup", "Vendor", "Warehouse"],
            ["فريق"] = ["User", "CustomerGroup"],
            ["فرع"] = ["Warehouse", "StateOrProvince"],
            ["فاتوره"] = ["Order"]
        };
}
