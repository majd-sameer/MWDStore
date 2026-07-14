namespace Store.Application.DevAssistant;

/// <summary>What kind of subject an intent can be about.</summary>
public enum SubjectKind
{
    Entity,
    Area,
    Topic,
    /// <summary>A name that is not expected to exist yet (NewModuleQuery).</summary>
    NewName,
    None
}

/// <summary>One intent in the registry: trigger lexemes with fixed weights (spec §3.2), bilingual.</summary>
public sealed record IntentDefinition(
    string Name,
    string Description,
    string DescriptionAr,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> ExamplesAr,
    IReadOnlyDictionary<string, int> Lexemes,
    bool RequiresSubject,
    IReadOnlyList<SubjectKind> AcceptedKinds)
{
    public string DescriptionFor(string culture) => culture == "ar" ? DescriptionAr : Description;

    public IReadOnlyList<string> ExamplesFor(string culture) => culture == "ar" ? ExamplesAr : Examples;
}

/// <summary>A resolved subject: an entity, an API area, or a knowledge-base topic — possibly all three facets.</summary>
public sealed record SubjectMatch(
    string DisplayName,
    EntitySnapshot? Entity,
    string? AreaSegment,
    string? TopicKey,
    int Tier,
    int TokensCovered)
{
    public bool Fits(IReadOnlyList<SubjectKind> kinds) =>
        (Entity is not null && kinds.Contains(SubjectKind.Entity))
        || (AreaSegment is not null && kinds.Contains(SubjectKind.Area))
        || (TopicKey is not null && kinds.Contains(SubjectKind.Topic));
}

/// <summary>The engine's full deterministic verdict for one query.</summary>
public sealed record QueryResolution(
    IntentDefinition? Intent,
    IReadOnlyList<IntentDefinition> AmbiguousIntents,
    SubjectMatch? Subject,
    IReadOnlyList<SubjectMatch> AmbiguousSubjects,
    IReadOnlyList<string> UnresolvedTokens,
    bool SubjectCarriedOver);

/// <summary>
/// The deterministic two-axis classifier (spec §3): resolves intent and subject independently by
/// arithmetic over exact/synonym/fuzzy token matches with fixed weights. No probabilistic ranking;
/// identical input against an identical snapshot yields an identical resolution.
/// </summary>
public sealed class IntentEngine
{
    private const int ScoreFloor = 2;
    private const int ScoreMargin = 1;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "how", "do", "does", "did", "i", "we", "the", "a", "an", "to", "me", "my", "our", "of",
        "for", "in", "on", "at", "is", "are", "was", "it", "its", "with", "please", "all", "any",
        "this", "that", "these", "those", "what", "which", "who", "when", "and", "or", "called",
        "named", "about", "like", "want", "need", "tell", "give", "get", "up", "there", "here",
        // Arabic function words, in folded form (see FoldArabic); checked both before and after
        // the definite-article strip so الذي/التي are caught either way.
        "كيف", "ما", "ماذا", "ماهي", "هي", "هو", "في", "من", "الي", "علي", "عن", "ان", "هل",
        "لي", "لدي", "مع", "او", "ثم", "كل", "هذا", "هذه", "ذلك", "يا", "يوجد", "توجد", "نظام",
        "باسم", "اسمه", "اسمها", "لماذا", "متي", "حتي", "قد", "لقد", "به", "بها", "له", "لها",
        "الذي", "التي", "ذي", "تي"
    };

    /// <summary>Words whose trailing 's' is not a plural marker.</summary>
    private static readonly HashSet<string> SingularizeExceptions = new(StringComparer.Ordinal)
    {
        "news", "status", "address", "cors", "https", "libs"
    };

    public static readonly IReadOnlyList<IntentDefinition> Intents =
    [
        new("ChangePathQuery",
            "Which files to touch, in order, to add or change a field on an existing entity — with the hard rules attached where they bite.",
            "أي الملفات تلمس، وبأي ترتيب، لإضافة أو تعديل حقل على كيان قائم — مع القواعد الصارمة مرفقة حيث تنطبق.",
            ["How do I add a new property to the categories module?", "Extend Product with a warranty field"],
            ["كيف أضيف خاصية جديدة إلى الفئات؟", "أضف حقل ضمان إلى المنتج"],
            Lex(("add", 4), ("extend", 4), ("modify", 3), ("change", 3), ("property", 3), ("field", 3), ("column", 2), ("new", 1),
                ("اضف", 4), ("اضيف", 4), ("اضافه", 4), ("عدل", 3), ("غير", 3), ("خاصيه", 3), ("حقل", 3), ("عمود", 2), ("جديد", 1), ("جديده", 1)),
            true, [SubjectKind.Entity]),
        new("NewModuleQuery",
            "The full scaffold checklist for a brand-new admin CRUD area, including the policy/AREA mirror step.",
            "قائمة البناء الكاملة لمنطقة إدارة جديدة كليًا، بما فيها خطوة مزامنة السياسة مع خريطة AREA.",
            ["How do I create a new module called departments?", "Add a new admin area"],
            ["كيف أنشئ وحدة جديدة باسم الأقسام؟", "أنشئ منطقة إدارة جديدة"],
            Lex(("create", 5), ("scaffold", 5), ("module", 4), ("entity", 4), ("area", 2), ("new", 1), ("add", 1),
                ("انشئ", 5), ("انشاء", 5), ("وحده", 4), ("موديول", 4), ("كيان", 4), ("جديد", 1), ("جديده", 1)),
            false, [SubjectKind.NewName, SubjectKind.Entity]),
        new("SchemaQuery",
            "The exact shape of an entity's table: columns, types, lengths, keys and indexes, straight from the EF model.",
            "الشكل الدقيق لجدول الكيان: الأعمدة والأنواع والأطوال والمفاتيح والفهارس، مباشرة من نموذج EF.",
            ["Show the columns of Category", "What does the Order table look like?"],
            ["اعرض أعمدة المنتج", "ما شكل جدول الطلب؟"],
            Lex(("schema", 5), ("column", 4), ("structure", 4), ("property", 3), ("field", 3), ("table", 3), ("definition", 3), ("look", 2), ("show", 2), ("list", 2), ("describe", 2),
                ("مخطط", 5), ("اعمده", 4), ("بنيه", 4), ("خصائص", 3), ("حقول", 3), ("جدول", 3), ("شكل", 2), ("اعرض", 2), ("اظهر", 2)),
            true, [SubjectKind.Entity]),
        new("RouteQuery",
            "Every API route for an area: verb, template, policy and audit participation, from the deployed binary.",
            "كل مسارات الـ API لمنطقة ما: الفعل والقالب والسياسة والمشاركة في التدقيق، من الملف التنفيذي المنشور.",
            ["Show me all routes for orders", "List the endpoints of the catalog area"],
            ["اعرض مسارات الطلبات", "ما مسارات منطقة الفئات؟"],
            Lex(("route", 5), ("endpoint", 5), ("api", 4), ("url", 3), ("controller", 3), ("expose", 2), ("show", 1), ("list", 1),
                ("مسار", 5), ("مسارات", 5), ("واجهه", 3), ("رابط", 3), ("روابط", 3), ("تحكم", 3), ("اعرض", 1), ("اظهر", 1)),
            true, [SubjectKind.Area, SubjectKind.Entity]),
        new("RelationQuery",
            "What links to an entity and what it links to: foreign keys and navigations, both directions.",
            "ما يرتبط بالكيان وما يرتبط به الكيان: المفاتيح الأجنبية وعلاقات التنقل في الاتجاهين.",
            ["What references Product?", "Foreign keys of Order"],
            ["ما الذي يشير إلى المنتج؟", "المفاتيح الأجنبية للطلب"],
            Lex(("reference", 4), ("depend", 4), ("relation", 4), ("relationship", 4), ("foreign", 4), ("fk", 4), ("navigation", 3), ("link", 3), ("key", 2),
                ("علاقه", 4), ("علاقات", 4), ("يشير", 4), ("تشير", 4), ("يعتمد", 4), ("اجنبي", 4), ("اجنبيه", 4), ("مفتاح", 2), ("مفاتيح", 2)),
            true, [SubjectKind.Entity]),
        new("LocateQuery",
            "Where the code for something lives, across every layer of the stack.",
            "أين يعيش الكود الخاص بشيء ما، عبر كل طبقات النظام.",
            ["Where is the code for wishlist?", "Which files implement categories?"],
            ["أين كود العلامات التجارية؟", "أي ملفات تنفّذ الفئات؟"],
            Lex(("where", 4), ("locate", 4), ("find", 3), ("file", 3), ("implement", 3), ("code", 2),
                ("اين", 4), ("وين", 4), ("ملف", 3), ("ملفات", 3), ("كود", 2), ("تنفذ", 2)),
            true, [SubjectKind.Entity, SubjectKind.Area, SubjectKind.Topic]),
        new("ConceptExplain",
            "How one of the codebase's fixed mechanisms works: the overlay, audit trail, auth, seeding, migrations, deployment.",
            "كيف تعمل إحدى آليات النظام الثابتة: طبقة الترجمة، سجل التدقيق، المصادقة، البذر، الترحيلات، النشر.",
            ["Explain the bilingual overlay", "How does the audit trail work?"],
            ["اشرح طبقة الترجمة", "كيف يعمل سجل التدقيق؟"],
            Lex(("explain", 4), ("work", 3), ("understand", 3), ("mean", 2),
                ("اشرح", 4), ("شرح", 4), ("يعمل", 3), ("تعمل", 3), ("فهم", 3), ("افهم", 3)),
            true, [SubjectKind.Topic]),
        new("RuleQuery",
            "The hard rules — the invariants you must not break.",
            "القواعد الصارمة — الثوابت التي يجب ألا تكسرها.",
            ["What are the hard rules?", "What must I never do here?"],
            ["ما هي القواعد الصارمة؟", "ما الممنوع فعله هنا؟"],
            Lex(("rule", 4), ("invariant", 4), ("forbidden", 3), ("must", 2), ("never", 2), ("hard", 2), ("dont", 2),
                ("قاعده", 4), ("قواعد", 4), ("ثوابت", 4), ("ممنوع", 3), ("محظور", 3), ("صارمه", 2), ("يجب", 2)),
            false, [SubjectKind.None]),
        new("CapabilityQuery",
            "What this assistant can answer.",
            "ما يستطيع هذا المساعد الإجابة عنه.",
            ["What can you do?", "help"],
            ["ماذا تستطيع أن تفعل؟", "مساعدة"],
            Lex(("help", 4), ("capability", 4), ("can", 2), ("you", 2), ("support", 2),
                ("مساعده", 4), ("ساعدني", 4), ("قدرات", 4), ("تستطيع", 2), ("تقدر", 2), ("تفعل", 2)),
            false, [SubjectKind.None])
    ];

    private static readonly Lazy<HashSet<string>> AllIntentVocabulary = new(() =>
        Intents.SelectMany(i => i.Lexemes.Keys).ToHashSet(StringComparer.Ordinal));

    /// <summary>True when the token is generic trigger vocabulary of ANY intent (e.g. a show-verb) —
    /// such a token is never a good miss-subject candidate.</summary>
    public static bool IsIntentVocabulary(string token) => AllIntentVocabulary.Value.Contains(token);

    private readonly SystemMetadataSnapshot _snapshot;
    private readonly Dictionary<string, IndexEntry> _subjectIndex;

    public IntentEngine(SystemMetadataSnapshot snapshot)
    {
        _snapshot = snapshot;
        _subjectIndex = BuildSubjectIndex(snapshot);
    }

    public QueryResolution Resolve(string text, IReadOnlyList<string>? contextSubjects = null)
    {
        var tokens = Tokenize(text);

        // ----- Axis 1: intent -----
        var scores = Intents
            .Select(intent => (Intent: intent, Score: tokens.Sum(t => intent.Lexemes.GetValueOrDefault(t))))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Intent.Name, StringComparer.Ordinal)
            .ToList();

        var top = scores[0];
        var second = scores[1];

        if (top.Score < ScoreFloor)
            return new QueryResolution(null, [], null, [], tokens, false);

        if (second.Score >= ScoreFloor && top.Score - second.Score < ScoreMargin)
            return new QueryResolution(null, [top.Intent, second.Intent], null, [], tokens, false);

        var intent = top.Intent;

        // ----- Axis 2: subject, from the tokens the winning intent did not consume -----
        var remaining = tokens.Where(t => !intent.Lexemes.ContainsKey(t)).ToList();
        var (subject, ambiguousSubjects, coveredTokens) = MatchSubject(remaining);
        var unresolved = remaining.Where(t => !coveredTokens.Contains(t)).ToList();

        // ----- Bounded follow-up resolution (spec §3.6): explicit subject always wins; only a
        // subject-less query borrows the most recent compatible subject from the client context. -----
        var carriedOver = false;
        if (subject is null && ambiguousSubjects.Count == 0 && intent.RequiresSubject && contextSubjects is not null)
        {
            foreach (var candidate in contextSubjects.Reverse())
            {
                var (contextMatch, _, _) = MatchSubject(Tokenize(candidate));
                if (contextMatch is not null && contextMatch.Fits(intent.AcceptedKinds))
                {
                    subject = contextMatch;
                    carriedOver = true;
                    break;
                }
            }
        }

        return new QueryResolution(intent, [], subject, ambiguousSubjects, unresolved, carriedOver);
    }

    // ---------------------------------------------------------------------------------------------
    // Subject matching: exact tier (1), synonym tier (2), bounded fuzzy tier (3). Candidates are
    // unigrams and joined adjacent bigrams; more covered tokens beat fewer, then lower tier wins.
    // A tie between two distinct subjects at the same grade is ambiguous — the engine asks (§3.2.3).
    // ---------------------------------------------------------------------------------------------

    private (SubjectMatch? Subject, IReadOnlyList<SubjectMatch> Ambiguous, IReadOnlySet<string> Covered)
        MatchSubject(IReadOnlyList<string> tokens)
    {
        var candidates = new List<(SubjectMatch Match, int Distance, IReadOnlyList<string> SourceTokens)>();

        void TryMatch(string candidate, IReadOnlyList<string> sourceTokens)
        {
            var key = NormalizeKey(candidate);
            if (key.Length == 0)
                return;

            if (_subjectIndex.TryGetValue(key, out var exact))
            {
                candidates.Add((ToMatch(exact, exact.IsSynonym ? 2 : 1, sourceTokens.Count), 0, sourceTokens));
                return;
            }

            var maxDistance = key.Length >= 8 ? 2 : key.Length >= 5 ? 1 : 0;
            if (maxDistance == 0)
                return;

            foreach (var (indexKey, entry) in _subjectIndex)
            {
                if (Math.Abs(indexKey.Length - key.Length) > maxDistance)
                    continue;
                var distance = Levenshtein(key, indexKey, maxDistance);
                if (distance <= maxDistance)
                    candidates.Add((ToMatch(entry, 3, sourceTokens.Count), distance, sourceTokens));
            }
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            TryMatch(tokens[i], [tokens[i]]);
            if (i + 1 < tokens.Count)
                TryMatch(tokens[i] + tokens[i + 1], [tokens[i], tokens[i + 1]]);
        }

        if (candidates.Count == 0)
            return (null, [], new HashSet<string>());

        var ordered = candidates
            .OrderByDescending(c => c.Match.TokensCovered)
            .ThenBy(c => c.Match.Tier)
            .ThenBy(c => c.Distance)
            .ThenBy(c => c.Match.DisplayName, StringComparer.Ordinal)
            .ToList();

        var best = ordered[0];
        var rivals = ordered
            .Where(c => c.Match.DisplayName != best.Match.DisplayName
                        && c.Match.TokensCovered == best.Match.TokensCovered
                        && c.Match.Tier == best.Match.Tier
                        && c.Distance == best.Distance)
            .Select(c => c.Match)
            .DistinctBy(m => m.DisplayName)
            .ToList();

        if (rivals.Count > 0)
            return (null, new[] { best.Match }.Concat(rivals).ToList(), new HashSet<string>());

        return (best.Match, [], best.SourceTokens.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Nearest real subjects by string distance, for the honest-miss flow (spec §3.5).</summary>
    public IReadOnlyList<string> NearestSubjects(string token, int count)
    {
        var key = NormalizeKey(Singularize(token.ToLowerInvariant()));
        return _subjectIndex
            .Where(kv => !kv.Value.IsSynonym)
            .Select(kv => (kv.Value.DisplayName, Distance: Levenshtein(key, kv.Key, int.MaxValue)))
            .GroupBy(x => x.DisplayName)
            .Select(g => (DisplayName: g.Key, Distance: g.Min(x => x.Distance)))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.DisplayName, StringComparer.Ordinal)
            .Take(count)
            .Select(x => x.DisplayName)
            .ToList();
    }

    public SubjectMatch? ResolveSubjectName(string name)
    {
        var (subject, _, _) = MatchSubject(Tokenize(name));
        return subject;
    }

    // --------------------------------------------------------------------------- index construction

    private sealed record IndexEntry(
        string DisplayName, EntitySnapshot? Entity, string? AreaSegment, string? TopicKey, bool IsSynonym);

    private static Dictionary<string, IndexEntry> BuildSubjectIndex(SystemMetadataSnapshot snapshot)
    {
        var index = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);

        void Add(string term, IndexEntry entry)
        {
            var key = NormalizeKey(Singularize(term.ToLowerInvariant()));
            if (key.Length < 2)
                return;
            if (index.TryGetValue(key, out var existing))
            {
                // Merge facets so "order" is one subject with both an entity and an area face.
                index[key] = new IndexEntry(
                    existing.DisplayName,
                    existing.Entity ?? entry.Entity,
                    existing.AreaSegment ?? entry.AreaSegment,
                    existing.TopicKey ?? entry.TopicKey,
                    existing.IsSynonym && entry.IsSynonym);
            }
            else
            {
                index[key] = entry;
            }
        }

        // Every entity CLR name and table name — generated, never hand-listed (spec §3.2.3).
        foreach (var entity in snapshot.Entities)
        {
            Add(entity.ClrName, new IndexEntry(entity.ClrName, entity, entity.HasAdminController ? entity.AdminAreaSegment : null, null, false));
            Add(entity.TableName, new IndexEntry(entity.ClrName, entity, null, null, false));
        }

        // Every API area segment (admin and storefront).
        foreach (var endpoint in snapshot.Endpoints)
        {
            var parts = endpoint.Route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var segment = parts.Length >= 3 && parts[0] == "api" && parts[1] == "admin" ? parts[2]
                : parts.Length >= 2 && parts[0] == "api" ? parts[1]
                : null;
            if (segment is null || segment.StartsWith('{'))
                continue;
            Add(segment, new IndexEntry(segment, null, segment, null, false));
        }

        // Knowledge-base topics and their synonyms.
        foreach (var topic in KnowledgeBase.Topics)
        {
            Add(topic.Key, new IndexEntry(topic.Key, null, null, topic.Key, false));
            foreach (var synonym in topic.Synonyms)
                Add(synonym, new IndexEntry(topic.Key, null, null, topic.Key, true));
        }

        // Curated cross-vocabulary synonyms resolving to already-indexed subjects.
        foreach (var (alias, target) in KnowledgeBase.SubjectSynonyms)
        {
            var targetKey = NormalizeKey(Singularize(target.ToLowerInvariant()));
            if (index.TryGetValue(targetKey, out var entry))
                Add(alias, entry with { IsSynonym = true });
        }

        return index;
    }

    private static SubjectMatch ToMatch(IndexEntry entry, int tier, int tokensCovered) =>
        new(entry.DisplayName, entry.Entity, entry.AreaSegment, entry.TopicKey, tier, tokensCovered);

    // --------------------------------------------------------------------------------- text pipeline

    /// <summary>
    /// Normalization (spec §3.2.1): lowercase, strip punctuation, tokenize, fold Arabic letter
    /// variants, strip the Arabic definite article, singularize, drop stop-words. Deterministic
    /// character rules only — no morphological analysis.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var raw in text.ToLowerInvariant())
        {
            var c = FoldArabic(raw);
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());

        var result = new List<string>(tokens.Count);
        foreach (var token in tokens)
        {
            if (StopWords.Contains(token))
                continue;
            var stripped = StripArabicArticle(token);
            if (StopWords.Contains(stripped))
                continue;
            result.Add(Singularize(stripped));
        }
        return result;
    }

    /// <summary>
    /// Folds Arabic letter variants to one canonical form so spelling variations match:
    /// hamza-carrying alifs → bare alif, taa marbuta → haa, alif maqsura → yaa. Also removes
    /// tatweel and harakat (diacritics) by mapping them to a separator.
    /// </summary>
    internal static char FoldArabic(char c) => c switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
        'ة' => 'ه',
        'ى' => 'ي',
        'ـ' => ' ',                       // tatweel
        >= 'ً' and <= 'ٟ' => ' ', // harakat / diacritics
        _ => c
    };

    /// <summary>Strips the Arabic definite article (ال) so «الفئات» matches «فئات».</summary>
    internal static string StripArabicArticle(string token) =>
        token.Length >= 4 && token.StartsWith("ال", StringComparison.Ordinal) ? token[2..] : token;

    private static string Singularize(string token)
    {
        if (token.Length <= 3 || SingularizeExceptions.Contains(token))
            return token;
        if (token.EndsWith("ies", StringComparison.Ordinal))
            return token[..^3] + "y";
        if (token.EndsWith("sses", StringComparison.Ordinal) || token.EndsWith("xes", StringComparison.Ordinal)
            || token.EndsWith("ches", StringComparison.Ordinal) || token.EndsWith("shes", StringComparison.Ordinal))
            return token[..^2];
        if (token.EndsWith("s", StringComparison.Ordinal) && !token.EndsWith("ss", StringComparison.Ordinal))
            return token[..^1];
        return token;
    }

    private static string NormalizeKey(string value) =>
        new(value.Select(FoldArabic).Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Plain DP Levenshtein with an early-exit bound — no recursion, no backtracking (SEC-8).</summary>
    internal static int Levenshtein(string a, string b, int bound)
    {
        if (a == b)
            return 0;
        if (a.Length == 0 || b.Length == 0)
            return Math.Max(a.Length, b.Length);

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (rowMin > bound)
                return bound + 1;
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    private static Dictionary<string, int> Lex(params (string Token, int Weight)[] lexemes) =>
        lexemes.ToDictionary(l => l.Token, l => l.Weight, StringComparer.Ordinal);
}
