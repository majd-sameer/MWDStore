# Developer Assistant Portal — Technical Specification & Functional Requirements

**Document type:** Technical Specification / Functional Requirements Document (FRD)
**Feature name:** Developer Assistant Portal (internal codename: *Deterministic Copilot*)
**Host application:** MyStore Admin Panel (Angular 22) + MyStore API (ASP.NET Core, .NET 10)
**Status:** Proposed — for review before implementation
**Related documents:** `docs/TECHNICAL-DOCUMENTATION.md` (developer handoff), `docs/architect-review.md`

---

## 1. Executive Summary & Project Objectives

### 1.1 Problem statement

MyStore is being handed off to the client's development team together with its source code and the developer handoff document. Experience with handoffs of this size shows a predictable support pattern: the inheriting team understands the system in the large but stalls on *mechanical wayfinding* questions — "which files do I touch to add a column to categories?", "what routes does the orders area expose?", "what is the exact shape of this table?". These questions do not require judgment; they require an accurate, current map of the system. Today that map lives in a static Markdown document that will drift the moment the team starts changing code.

### 1.2 Proposed solution

Embed a **Developer Assistant Portal** inside the Admin panel: a chat-style interface that answers structural questions about the running system **deterministically**, using the application's own metadata as the source of truth:

- the **EF Core model** (`StoreDbContext.Model`, the `IModel` metadata graph) for entities, tables, columns, keys, relationships and indexes;
- **C# reflection** over the `Store.Api` assembly for controllers, attribute routes, HTTP verbs and authorization policies;
- a **hand-authored knowledge layer** encoding this codebase's fixed conventions (the layer-by-layer change path, the bilingual overlay rules, the hard rules from §18 of the handoff document).

The interface *presents* like an AI copilot — a conversational input, rich reply cards — but *behaves* like a compiler: the same question always yields the same answer, the answer is computed from the deployed binary, and no answer is ever invented.

### 1.3 The Zero-LLM constraint (non-negotiable)

The feature must run on the existing low-spec production topology (IIS + internal Kestrel + SQL Server) with:

- **no local model runtimes** (no Ollama, llama.cpp, ONNX inference, or similar);
- **no external AI/API calls** (no OpenAI, Gemini, Anthropic, or any outbound network dependency) — production has no CORS and should gain no new egress;
- **no GPU, no elevated RAM/CPU envelope** — the intent engine is string tokenization plus dictionary lookups; the metadata snapshot is built once and cached;
- **no non-determinism** — identical input against an identical assembly produces byte-identical output. This is a *feature*: answers are testable, cacheable and trustworthy in a way generative output is not.

### 1.4 Objectives and measurable success criteria

| # | Objective | Success measure |
|---|---|---|
| O1 | Developer self-sustainability | The client team can answer "what files change for X" and "what does table/route Y look like" without contacting the original author |
| O2 | Always-current documentation | Schema and route answers are derived from the deployed assembly and EF model, never from static text — zero possibility of doc drift for those answer types |
| O3 | Self-discovery of new modules | When the client team adds a new entity (e.g. a future `Department`), it becomes queryable in the portal with **zero portal configuration** |
| O4 | Guardrail transmission | Every change-path answer carries the codebase's hard rules (audit trail, overlay writer, DTO contract) inline, at the moment of relevance |
| O5 | Zero operational cost | p95 response time ≤ 100 ms on the production server; steady-state memory for the metadata snapshot ≤ 10 MB; no new services, processes or dependencies |

### 1.5 Explicit non-goals

- **Not a code generator.** The portal names the files and describes each edit; it never writes, patches or scaffolds code, and it never executes migrations.
- **Not a data browser.** It reads *metadata* (column definitions), never *rows*. It must be impossible to retrieve customer, order or credential data through it.
- **Not a general chatbot.** Out-of-scope questions get an honest "not something I can answer" plus the capability catalog — never an improvised reply.
- **Not bilingual.** Unlike the customer-facing product, this is an internal developer tool; the UI and answers are English-only and it does not participate in the content-overlay system.

---

## 2. System Architecture Design

### 2.1 Placement within the existing architecture

The feature adds one vertical slice that follows every existing convention of the codebase:

| Layer | New component | Mirrors existing pattern |
|---|---|---|
| `Store.Api` | `AdminDevAssistantController` at `/api/admin/dev-assistant`, guarded by a new policy `area:dev-assistant` | One `Admin<Area>Controller` per admin area (§6 of the handoff doc) |
| `Store.Api/Models` | Response DTO records for the structured chat reply | DTO-records-as-public-contract rule |
| `Store.Application` | `DevAssistant/` folder: `SystemMetadataProvider`, `IntentEngine`, `KnowledgeBase`, `AnswerComposer` | Business logic lives in Application services, wired in `AddStoreApplication()` |
| `web/projects/data-access` | `lib/admin/admin-dev-assistant.service.ts` + model interfaces in `models.ts` | One typed service per admin controller, models mirrored in the same commit |
| `web/projects/admin` | `features/dev-assistant/` lazy route, sidebar entry, chat components | Lazy feature areas with `roleGuard`, sidebar filtered by role |

No new projects, hosted services, background workers or storage. The feature is stateless on the server beyond one cached, immutable metadata snapshot.

### 2.2 End-to-end request flow

1. **Input.** The developer types a query into the chat panel in the Admin app. The Angular component posts it (plus a short rolling window of prior turns for follow-up resolution, see §3.6) through `AdminDevAssistantService` to `POST /api/admin/dev-assistant/query`. The request passes through the standard five-interceptor chain — correlation id, accept-language, base-url, bearer token, error handling — exactly like every other admin call.
2. **Gatekeeping.** The controller's `area:dev-assistant` policy authorizes the caller (§6.1). The action is decorated to skip the audit *write* filter's generic entry and instead records its own purpose-built audit entry (§6.4), consistent with the `[SkipAudit]`-plus-richer-entry pattern already used by stock-out.
3. **Intent resolution.** The `IntentEngine` normalizes the text, extracts intent + subject candidates (§3), and resolves subjects against the live metadata snapshot.
4. **Metadata retrieval.** The matched intent handler pulls from the `SystemMetadataProvider` — the cached snapshot described in §2.3 — and/or the static `KnowledgeBase` (convention templates, hard rules).
5. **Composition.** The `AnswerComposer` assembles a **structured reply**: an ordered list of typed content blocks (§2.5). No free-form prose is generated server-side beyond parameterized template sentences.
6. **Rendering.** The Angular chat component pattern-matches on each block's discriminator and renders the corresponding rich component — checklist, property grid, endpoint matrix, callout, plain text (§4).

Every step is synchronous, in-process and read-only. There is no streaming (answers are instant), no queue, and no persistence of conversation state on the server.

### 2.3 The System Metadata Provider

`SystemMetadataProvider` is the single source of structural truth. It is registered as a singleton and builds an **immutable snapshot** on first use, then serves it for the lifetime of the process.

**Source A — EF Core model metadata.** Read from `IModel` (obtained at snapshot-build time from a scoped `StoreDbContext` via `IServiceScopeFactory`; the design-time model is identical because mapping lives entirely in the `IEntityTypeConfiguration<T>` classes). Per entity type the snapshot records:

- CLR type name and namespace, mapped table name;
- every scalar property: name, CLR type, store/SQL type, max length, nullability, default, whether it is part of the primary key, whether it is a foreign key and to which principal;
- navigations: target entity, cardinality, delete behavior;
- indexes: name, columns, uniqueness, filter expression (e.g. the filtered signature-product index);
- marker-interface facts derived by reflection on the CLR type: implements `ISoftDeletable` (soft-delete discipline applies), `ISeoEntity` (slug/SEO column block), `IAuditedEntity` (timestamp columns).

**Source B — API surface via reflection.** A one-time scan of the `Store.Api` assembly for `ControllerBase` descendants, harvesting per action: controller name, route template (composed from controller + method attributes), HTTP verb, authorization requirement (`[Authorize(Policy = …)]` / `[AllowAnonymous]`), presence of `[SkipAudit]`, and the parameter/return DTO type names. This yields the live equivalent of the handoff document's route catalog, guaranteed to match the deployed binary.

**Source C — cross-layer correlation by convention.** The codebase's naming conventions are regular enough to compute, not hand-maintain, the mapping between layers. For an entity/area *X* the provider derives the expected artifact set: domain file `Store.Domain/X.cs`; configuration `Store.Data/Configurations/XConfiguration.cs`; controller `AdminXsController` and its DTO records; frontend service `lib/admin/admin-xs.service.ts`; admin feature folder `features/xs`. Each derived path is tagged *verified* (the reflected type or a matching configuration class actually exists) or *expected* (convention says it should be there — used when describing files to *create*). The frontend-side paths are always *expected*-grade, since the API cannot reflect over the Angular workspace; the knowledge base pins the convention and the answer says so explicitly.

**Source D — the curated Knowledge Base.** Facts that cannot be derived from metadata and must be authored once, versioned with the code (a strongly-typed static registry in `Store.Application/DevAssistant/`, unit-testable):

- the canonical **change-path templates** — ordered step lists for "add a property", "add a new admin CRUD area", "add a bilingual field", "add a storefront CMS page" — parameterized by entity name and conditioned on entity facts (bilingual? soft-deletable? has admin controller?);
- the **hard rules** (handoff doc §18) attached as warnings to the steps where they bite (e.g. the overlay-writer staging rule attaches to the controller step of any bilingual change path);
- fixed operational facts: the `build:libs` requirement, the migrations command line, the policy/AREA mirror rule;
- the **synonym dictionary** for intent matching (§3.3).

**Snapshot lifecycle.** Built once per process start, treated as immutable thereafter. This is deliberate: the snapshot then *always* describes the running binary (§6.5). The snapshot carries a fingerprint — assembly informational version, EF model hash (a stable hash over the ordered entity/property definitions), process start time — surfaced in the UI so a developer can see exactly which build is answering.

### 2.4 API surface

Two endpoints, both under `area:dev-assistant`:

| Verb & route | Purpose |
|---|---|
| `POST /api/admin/dev-assistant/query` | Submit a query (text + optional prior-turn context); returns a structured `AssistantReply` |
| `GET /api/admin/dev-assistant/capabilities` | Returns the capability catalog (supported intents with example phrasings) and the snapshot fingerprint; the UI uses it for the welcome message, "did you mean" suggestions and an autocompletion hint list |

Both are reads in effect; the query endpoint is `POST` only because queries with context exceed comfortable query-string size. Responses use the standard DTO-record conventions and the standard `{ error: … }` failure shape.

### 2.5 The structured reply contract

The chat is *not* a text channel. A reply is an ordered sequence of typed **content blocks**, each a DTO record with a string discriminator, mirrored one-to-one in `data-access/models.ts`:

| Block type | Payload (summary) | Rendered as (§4) |
|---|---|---|
| `text` | template-composed sentence(s), optional emphasis spans | chat paragraph |
| `checklist` | ordered steps: layer tag, file path (+ verified/expected flag), edit description, optional command line, optional attached warning ids | interactive file-modification checklist |
| `propertyGrid` | entity header (CLR name, table, marker interfaces) + rows: column, CLR type, SQL type, length, nullable, key/FK/index badges | property pill grid |
| `endpointMatrix` | rows: verb, route, action, policy, audited flag | routing matrix table |
| `callout` | severity (info/warning/critical), rule id, rule text, handoff-doc section reference | highlighted guardrail card |
| `suggestions` | ranked alternative queries (for misses and ambiguity) | tappable chips that resubmit |

Adding a new block type is a backend + `models.ts` + renderer change — governed by the same "public contract, one commit" rule as every other DTO.

---

## 3. Behavioral Intent Mapping

### 3.1 Design philosophy

The engine is a **deterministic two-axis classifier**: it independently resolves *what the user wants to do* (the **intent**) and *what they want it done about* (the **subject**), then dispatches to the handler registered for that intent. There is no probabilistic ranking model; scoring is arithmetic over exact/synonym/fuzzy token matches with fixed weights, so behavior is fully unit-testable and identical across environments.

### 3.2 Processing pipeline

1. **Normalization.** Lowercase; strip punctuation; collapse whitespace; tokenize on word boundaries; singularize plural tokens with a small rule table (s/es/ies) — "categories" → "category", "departments" → "department". Stop-words ("how", "do", "i", "the", "a", "to", "me") are removed for matching but retained for pattern templates that rely on them.
2. **Intent detection.** Each intent in the registry declares trigger lexemes with weights (verbs and verb-synonyms strong, supporting nouns weak). The token set is scored against every intent; the top score wins if it clears an absolute floor *and* leads the runner-up by a defined margin — otherwise the query is *ambiguous* and both candidates come back as suggestion chips (§3.7).
3. **Subject resolution.** Remaining tokens are matched against the **subject index**, which is generated from the metadata snapshot (never hand-listed): every entity CLR name, table name, admin area segment ("products", "news", "tax", …) and knowledge-base topic ("migration", "overlay", "audit", "deployment"), each with its synonym expansions. Matching is tiered — exact, then synonym, then bounded fuzzy (edit distance scaled to token length, max 2) to absorb typos like "catagories". Tie between two subjects at equal tier → ambiguous, ask.
4. **Dispatch.** The (intent, subject) pair routes to a handler; each handler validates that the subject *kind* fits (a `SchemaQuery` needs an entity; a `ConceptExplain` needs a topic) and otherwise degrades to the miss flow.

### 3.3 Intent taxonomy (initial release)

| Intent | Trigger examples | Subject kind | Answer composition |
|---|---|---|---|
| `ChangePathQuery` | "how do I **add** a new **property/field/column** to X", "extend X with…" | entity | `checklist` from the add-field template, parameterized with X's real file paths; bilingual branch auto-included when X participates in the overlay system; `callout`s for the hard rules touched |
| `NewModuleQuery` | "how do I **create/add a new module/area/entity**" | entity name (may be unknown — that's expected) | `checklist` from the new-admin-area template, all steps *expected*-grade, including the policy/AREA mirror step |
| `SchemaQuery` | "**show/list** the **columns/fields/schema/properties** of X", "what does the X table look like" | entity | `propertyGrid` straight from the EF model snapshot |
| `RouteQuery` | "**show/list** all **routes/endpoints/APIs** for X" | area or entity | `endpointMatrix` filtered to the area; includes each route's policy and audit participation |
| `RelationQuery` | "what **references/links to/depends on** X", "foreign keys of X" | entity | `propertyGrid` variant scoped to FKs + navigations, both directions |
| `LocateQuery` | "**where is** the code for X", "which files implement X" | entity/area/topic | `checklist` (non-interactive variant) of correlated files across all layers |
| `ConceptExplain` | "**explain/how does** the **overlay/audit/auth/seeding** work" | knowledge-base topic | `text` + `callout` blocks from curated content, with pointers into `TECHNICAL-DOCUMENTATION.md` sections |
| `RuleQuery` | "what are the **hard rules/things I must not do**" | none | the ten §18 invariants as `callout` blocks |
| `CapabilityQuery` | "what can you do", "help" | none | capability catalog as `suggestions` |

The registry is open for extension: adding an intent is one handler class plus registry entry plus tests, no engine changes.

### 3.4 Worked example — a subject that exists: *categories*

Query: *"How do I add a new property to the categories module?"*

- Normalization yields tokens including *add, new, property, category, module*.
- *add + property* scores `ChangePathQuery` decisively (the "new" and "module" tokens add nothing to competing intents).
- *category* resolves at the exact tier to the `Category` entity in the snapshot.
- The handler reads Category's facts from the snapshot: it is a real entity mapped to the `Category` table; it implements `ISeoEntity` and `ISoftDeletable`; it *is* bilingual (the knowledge base flags category name/description as overlay-participating, and the correlation layer confirms `AdminCategoriesController` exists).
- The reply composes: a one-line `text` framing; a `checklist` instantiating the add-field template with the *actual* paths (`Store.Domain/Category.cs` → `Store.Data/Configurations/CategoryConfiguration.cs` → the migrations command → `Store.Api/Models/AdminModels.cs` + `AdminCategoriesController` projection/apply → `web/projects/data-access/src/lib/models.ts` → the admin categories form) each tagged *verified*; a conditional bilingual sub-step (the `…En` pair, `LocalizedProperty` constant, `multi-lang-input`) because the entity is overlay-participating; `callout`s for hard rules 1 (no bulk operators), 2 (writer never saves), 5 (DTO contract, same-commit mirror) and 8 (`build:libs`) attached at the steps where each applies.

### 3.5 Worked example — a subject that does not exist yet: *departments*

Query: *"Show me all routes for departments."*

- Intent resolves to `RouteQuery` (*show, routes*).
- *department* fails exact and synonym tiers against the subject index; fuzzy matching finds no in-bounds candidate.
- The handler **must not fabricate**. It returns: a `text` block stating plainly that no entity, table or API area named "department" exists in the deployed build (citing the snapshot fingerprint); a `suggestions` block with the nearest real subjects by string distance and, deterministically ranked first if applicable, semantically adjacent areas from the synonym dictionary (e.g. *customer-groups*, *vendors*, *warehouses* — the structures a newcomer might mean by "departments"); and one tappable escalation chip: *"How do I create a new module called departments?"* — which routes to `NewModuleQuery` and yields the full scaffold checklist.
- **The self-discovery guarantee (objective O3):** the moment the client team ships a `Department` entity with its configuration and an `AdminDepartmentsController`, the next process start rebuilds the snapshot and the same query returns a populated `endpointMatrix` — with zero changes to the assistant itself. This behavior is the feature's core promise and must be covered by an integration test that registers a synthetic entity and asserts discovery.

### 3.6 Follow-up resolution (bounded, deterministic)

True conversational memory is out of scope, but one cheap mechanism covers the dominant follow-up pattern: the client sends the last N (proposed: 3) turns' resolved subjects with each query. If the new query yields an intent but **no** subject (*"and its columns?"*, *"what about the routes?"*), the engine substitutes the most recent compatible subject from that context and says so in the reply's framing sentence ("Columns of **Category** — carried over from your previous question"). If the query contains an explicit subject, context is ignored. No server-side session state is created.

### 3.7 Miss and ambiguity behavior (summary; details §6.2)

- **Unknown intent:** capability catalog as suggestions. Never a guessed answer.
- **Known intent, unknown subject:** the §3.5 flow — honest miss, nearest matches, escalation chip.
- **Ambiguous either axis:** both leading candidates as suggestion chips phrased as full queries; tapping resubmits. The engine never silently picks between near-ties.

---

## 4. Frontend UI/UX Functional Requirements

### 4.1 Placement and shell

- **FR-UI-1.** The portal is a lazy-loaded admin feature at `/dev-assistant`, guarded by `authGuard` + `roleGuard` for the mirrored `AREA['dev-assistant']` entry, with a sidebar item (wrench/terminal icon) visible only to authorized roles — consistent with every other feature area.
- **FR-UI-2.** The screen is a full-height chat layout: scrollable transcript, pinned composer at the bottom (text input, send button, Enter submits, Shift+Enter for newline), and a slim header showing the snapshot fingerprint (build version + model hash + started-at) with a tooltip explaining that answers describe *this deployed build*.
- **FR-UI-3.** **Dark theme, scoped.** The portal renders in a dark "terminal" theme regardless of the rest of the admin chrome, to visually mark it as developer tooling. All colors derive from a scoped token set layered on the `ui` library's SCSS tokens (`_tokens.scss`) — a `.dev-assistant-theme` scope, not global overrides — so the theme cannot leak into other admin screens. Contrast must meet WCAG AA on the dark palette.
- **FR-UI-4.** On first open (empty transcript) the panel shows a welcome card built from the `capabilities` endpoint: a short statement of what the assistant is (deterministic, metadata-driven, no AI), followed by example queries as tappable chips.

### 4.2 Message flow behavior

- **FR-UI-5.** User messages render right-aligned as plain bubbles; assistant replies render left-aligned as a **card stack** — one card per content block, in server order. The bubble metaphor visibly "transforms": a reply that contains a `propertyGrid` widens to a structured card rather than wrapping prose.
- **FR-UI-6.** Responses are near-instant (deterministic backend); no typing animation or artificial delay is permitted — fake latency would misrepresent the mechanism. A standard loading state covers genuine network time only.
- **FR-UI-7.** Rendering dispatches on each block's discriminator via a typed map; an unrecognized discriminator (client older than server) renders a graceful fallback card showing the block's `text` summary field, which every block type must carry precisely for this purpose.
- **FR-UI-8.** Transcript state is client-side only, held in a signal store, persisted to `sessionStorage` (survives route changes and reloads within the tab, dies with the tab). A "clear conversation" control empties it. Nothing conversational is ever written to `localStorage` or the server.

### 4.3 Block component requirements

**Interactive file-modification checklist (`checklist`)**

- **FR-UI-9.** Renders ordered steps, each showing: a **layer tag pill** (Domain / Data / Migration / API / data-access / Admin UI — color-coded consistently across the portal), the file path in monospace with a copy-to-clipboard affordance, and the edit description. Steps carrying a command line (e.g. the migrations command) show it in a monospace strip with its own copy button.
- **FR-UI-10.** Each step has a checkbox. Checking is purely visual progress-tracking for the developer working through the list; state lives with the transcript entry in the session store. A progress indicator ("3 of 7") sits in the card header. Checking a step has **no** server effect.
- **FR-UI-11.** Steps flagged *expected* (convention-derived, file may not exist yet — always true for files the developer is being told to create) render a subtle "to create / verify path" badge, distinguishing them from *verified* steps whose backend artifacts were confirmed by reflection.
- **FR-UI-12.** A step with attached warnings renders the warning inline, directly beneath the step (severity-tinted left border), not merely at the card's end — the guardrail must be visible at the moment the developer reads the step it protects.

**Property pill grid (`propertyGrid`)**

- **FR-UI-13.** Header row: entity CLR name, mapped table name, and marker-interface pills (`ISoftDeletable`, `ISeoEntity`, `IAuditedEntity`, "bilingual overlay") when applicable.
- **FR-UI-14.** One row per column: name (monospace), CLR type, SQL type + length, nullability, and badge pills for PK / FK (with principal entity, which is tappable and submits a `SchemaQuery` for that entity) / indexed / unique. Rows are text-filterable client-side when the entity has more than ~15 columns.
- **FR-UI-15.** The grid is horizontally scroll-contained within its card on narrow viewports; the transcript never scrolls horizontally.

**Endpoint routing matrix (`endpointMatrix`)**

- **FR-UI-16.** Columns: HTTP verb (color-coded pill: GET/POST/PUT/DELETE), route template (monospace, copyable), action name, required policy, and an "audited" indicator reflecting the action's audit participation. Sorted by route then verb; client-side filter by verb and free text.
- **FR-UI-17.** Where Swagger is available (development), each row may deep-link to the corresponding Swagger operation; the link column is hidden in production, where Swagger is intentionally absent.

**Guardrail callout (`callout`)**

- **FR-UI-18.** Severity-tinted card (info / warning / critical) carrying the rule text and a reference chip pointing at the relevant handoff-document section. Critical callouts (the §18 invariants) use a distinct treatment that cannot be confused with informational notes.

**Suggestion chips (`suggestions`)**

- **FR-UI-19.** Rendered as a horizontal wrap of tappable chips; tapping submits the chip's full query text as a new user message (visible in the transcript, so the conversation stays honest about what was asked).

### 4.4 Frontend architecture requirements

- **FR-UI-20.** All HTTP goes through a new `AdminDevAssistantService` in `data-access` following house convention — the `capabilities` read as an `httpResource`, the query submission as an `Observable` command — with every reply interface added to `models.ts` in the same commit as the backend DTOs (hard rule 5).
- **FR-UI-21.** Components are standalone + signals + zoneless-safe, matching the workspace. Presentational block components live in the feature folder; any genuinely reusable primitive (the copy-to-clipboard affordance, the verb pill) belongs in the `ui` library — and library changes require `npm run build:libs` before they take effect (hard rule 8).
- **FR-UI-22.** The portal is exempt from the ngx-translate bilingual requirement (English-only developer tool, §1.5) but must not break the admin shell's RTL mode: when the surrounding chrome is RTL, the portal region forces LTR (code, paths and tables are directional content).

---

## 5. Edge Cases & Administrative Security

### 5.1 Access control

- **SEC-1.** A new policy `area:dev-assistant` is added to `AuthPolicies` and registered in `AddStorePolicies`. Initial membership: **`super-admin` only** — deliberately tighter than the usual super-admin + admin pair, because the portal reveals the complete schema and route/policy topology, which is reconnaissance-grade information. Widening membership (e.g. a future dedicated `developer` role) is a two-line, two-file change.
- **SEC-2.** Per hard rule 10, the same entry is added to the admin app's `AREA` map in `roles.ts` in the same commit, so the sidebar item, route guard and API policy stay one permission model.
- **SEC-3.** Frontend guards are UX, not security: authorization is enforced by the policy on the controller. A direct API call without the role receives the standard 403.
- **SEC-4.** Defense-in-depth kill switch: a single configuration flag (`DevAssistant:Enabled`, default true) lets an operator disable the endpoints entirely (404) without a code change — the fifth configuration section, documented alongside the existing four.

### 5.2 Unrecognized and adversarial input

- **SEC-5.** Query text is length-capped (proposed 500 characters), and the context window capped at 3 turns; oversize input is rejected with the standard error shape. The text is tokenized only — never evaluated, never interpolated into SQL/LINQ, never reflected upon as a type name from raw user input (subject resolution matches user tokens **against** the snapshot's known-name index; user text is never fed to `Type.GetType` or model lookup APIs directly).
- **SEC-6.** All user-originated strings echoed into replies are treated as untrusted data end-to-end and rendered through Angular's default binding (no `innerHTML`), foreclosing self-XSS via a crafted "query".
- **SEC-7.** Misses are honest (§3.7): unknown intent → capability catalog; unknown subject → explicit "does not exist in this build" plus nearest matches; ambiguity → ask via chips. The engine has no fallback that guesses, because a wrong-but-confident answer about a change path is worse than no answer.
- **SEC-8.** The engine is O(tokens × index size) dictionary matching with bounded edit-distance — no backtracking regex over user input, no recursion driven by input shape — so pathological input cannot induce CPU amplification. Standard model validation covers malformed payloads.

### 5.3 Information exposure boundaries

- **SEC-9.** **Metadata only, ever.** No handler may query entity *data*; the assistant's Application services take no dependency through which row data could flow to a reply (the metadata snapshot is fully materialized up front and the `DbContext` is not reachable from intent handlers). This makes "leak customer data through the chat" structurally impossible rather than merely forbidden.
- **SEC-10.** Even at the metadata level, a deny-list mirrors `AuditSecrets`: properties whose names match the sensitive patterns (password, secret, token, apikey — e.g. `User.PasswordHash`, `RefreshTokenHash`, `PaymentProvider.AdditionalSettings`) appear in property grids by **name only**, tagged *sensitive*, with type/length/default details suppressed and a callout noting they are audit-excluded. The route matrix never includes example payloads or configuration values.
- **SEC-11.** Replies never include server filesystem paths (only repository-relative source paths), connection information, or configuration values.

### 5.4 Auditability

- **SEC-12.** The query action opts out of the generic `AuditActionFilter` entry (it is a read in effect and would otherwise be misclassified as an admin write on POST) and instead writes its own audit entry per query via `IAuditService` — actor, timestamp, the query text, resolved intent/subject, and hit/miss — following the established richer-entry pattern. This gives the client team a usage record and gives security review a trail of who explored the system's structure and when.

### 5.5 Managing assembly and structural change safely

- **SEC-13.** **Snapshot immutability = truthfulness.** The snapshot is built once per process start and never mutated, so it describes the running binary by construction. Deploys recycle the app pool, which rebuilds it; there is no runtime refresh endpoint (a "refresh" that re-reflects the *same* loaded assembly is a no-op that only invites false confidence, and hot-swapping assemblies is not part of this deployment model).
- **SEC-14.** Every structural answer is stamped with the snapshot fingerprint (§2.3) in the reply metadata, and the UI header shows it persistently — a developer comparing an answer against a *newer* working tree can see at a glance that the portal describes the deployed build, not their branch.
- **SEC-15.** **Startup resilience.** Snapshot construction is defensive: failure to reflect one controller or resolve one convention correlation degrades that answer domain (with an explicit "partially unavailable" notice in affected replies) rather than failing application startup. The assistant must never be able to take the store down.
- **SEC-16.** **Rename/refactor tolerance.** Convention-derived correlations (Source C) can break when the client team deviates from naming conventions. Because each step carries its verified/expected grade (FR-UI-11), a broken correlation degrades to *expected* with a "verify this path" badge — visibly less certain, never silently wrong. The knowledge base's authored templates are covered by unit tests that fail the build if a referenced convention anchor (e.g. the `AdminBrandsController` exemplar) disappears, so refactors surface the doc-rot at compile/CI time.
- **SEC-17.** **Pending-migration awareness (single sanctioned DB touch).** At snapshot build, one metadata-catalog query compares the migrations assembly against the `__EFMigrationsHistory` table. If pending migrations exist, every `SchemaQuery` reply carries a critical callout: *the code model and the database schema disagree; apply migrations before trusting column answers.* This is the only database access the feature performs, it runs once per process start, and it reads the history table only.

### 5.6 Testing requirements

- **TEST-1.** Engine determinism: a golden-file test suite of query → (intent, subject, block sequence) fixtures; any diff is a reviewed change. Includes the "categories" and "departments" worked examples verbatim.
- **TEST-2.** Self-discovery: an integration test registering a synthetic entity + controller in a test host asserts it becomes resolvable with zero assistant changes (O3).
- **TEST-3.** Exposure boundaries: tests assert sensitive-property suppression (SEC-10) and that no handler can reach row data (SEC-9, enforced by construction and pinned by test).
- **TEST-4.** Frontend: renderer dispatch (including the unknown-block fallback, FR-UI-7) and checklist state behavior get Vitest specs — this feature should raise, not follow, the workspace's thin spec coverage.

---

## 6. Delivery notes

**Suggested build order** (each stage independently shippable): ① `SystemMetadataProvider` + `capabilities` endpoint + fingerprint (foundation, testable alone) → ② `SchemaQuery` + `RouteQuery` with the property grid and endpoint matrix (highest value per effort, pure metadata) → ③ the knowledge base + `ChangePathQuery`/`NewModuleQuery` checklists with guardrail callouts (the differentiating feature) → ④ `RelationQuery`, `LocateQuery`, `ConceptExplain`, follow-up context (polish).

**Documentation obligations on completion:** add the new policy to §7.2's table and the `DevAssistant:Enabled` flag to §7.5's configuration list in `TECHNICAL-DOCUMENTATION.md`; add the portal itself to §15's feature list — the assistant documents the system, and the system's documentation must in turn document the assistant.
