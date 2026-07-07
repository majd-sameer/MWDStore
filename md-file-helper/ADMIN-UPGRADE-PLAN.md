# MyStore — Admin Upgrade Plan (CMS + E-commerce + Audit Log, Role-Based)

> **How to use this file with Claude Code.** Keep this file at the repo root next to
> `PROJECT-OVERVIEW.md` and `CLAUDE.md`. Work **one phase per Claude Code session** and start each
> session with: *"Read `PROJECT-OVERVIEW.md` and `ADMIN-UPGRADE-PLAN.md`, then implement Phase N.
> Do not start other phases."* Each phase ends with a **Definition of Done** checklist — ask Claude
> Code to verify every item (build, `dotnet test`, `npm run lint`, manual smoke steps) before
> committing. Phases are ordered by dependency; do not reorder.

---

## 0. Scope summary (what we are building)

| # | Requirement | Solution in this plan |
|---|---|---|
| 1a | Admin reorganized into 5 sections: **Stock management, Content management, Sales, People, System** | Phase 1 — sidebar/IA restructure mapped onto existing `AREA.*` role sets |
| 1b | Full **audit log** of admin actions, viewable per role | Phase 2 — `AuditLog` entity + auto-capture filter + System → Audit Log page |
| 1c | **Stock-out cases** (Sale, Gift, Matched, Third-party, External event, Reserved, For display only) + **who** removed the product + **sales channel** (Showroom, External Exhibition, External Broker, Local Broker, Online Store) | Phase 3 — stock-out workflow on top of `Stock`/`StockHistory` |
| 2 | **Unique/Signature products**: shown in storefront **before** Best Sellers, sorted first in `/shop`, with a **special card design** | Phase 4 |
| 3 | **CMS static-text editing**: admins edit copy + media of storefront blocks (e.g. `hero-grid` → `hero-copy` text, `hero-media` image) without touching design | Phase 5 — keyed `ContentBlock` system, bilingual |
| — | Hardening, tests, seed data, docs | Phase 6 |

**Global constraints (apply to every phase):**

- Bilingual EN/AR everywhere — every new label goes into
  `web/projects/<app>/src/assets/i18n/{en,ar}.json`; every user-visible DB content field must be
  translatable via `LocalizedContentProperty` or explicit `*_Ar`/`*_En` columns (this plan uses the
  existing `LocalizedContentProperty` overlay pattern for CMS blocks).
- RTL-safe: CSS logical properties only, as the codebase already does.
- Keep server policies (`AuthPolicies.cs`) and client role sets (`admin/src/app/core/roles.ts`)
  **in sync** — every phase that touches roles lists both files.
- Seeders stay **idempotent and additive** (insert-if-missing, never update), matching the existing
  seeder contract.
- EF Core migrations: one migration per phase, named as given in the phase.
- All admin GET lists reuse the global list classes in `projects/admin/src/styles.scss`
  (`.list-toolbar`, `.filter-chip`, `.list-pager`, `.action-btn`, `.empty-state`).

---

## Phase 1 — Admin information architecture: the 5 sections

**Goal:** regroup the existing admin console into the five requested sections without changing any
backend permissions yet. This is a **frontend-only** phase (plus one role-set addition).

### 1.1 Section → feature mapping

| New sidebar section | Existing features it contains | Visible to (role sets from `roles.ts`) |
|---|---|---|
| **Stock management** | inventory, warehouses, shipping, shipments, *(Phase 3 adds: stock-out, stock-out log)* | `AREA.Inventory` ∪ `AREA.Fulfillment` |
| **Content management** | products, categories, brands, catalog-settings, cms (pages/menus/news), moderation, media, *(Phase 5 adds: content-blocks)* | `AREA.Catalog` ∪ `AREA.Content` |
| **Sales** | dashboard, orders, customers, customer-groups, contacts, promotions, tax, payments, vendors | `AREA.Sales` ∪ `AREA.Marketing` ∪ `AREA.Reports` |
| **People** | users (staff + roles), customers *(link duplicated from Sales is OK — same route)* | `AREA.Users` (users) / `AREA.Sales` (customers) |
| **System** | settings, localization, locations, logs, *(Phase 2 adds: audit-log)* | `AREA.Settings` |

> Note: products/categories live under **Content management** because `content-writer` already owns
> Catalog + Content in the access matrix, and merchandising copy is content work. If the business
> later disagrees, moving a nav link is a one-file change — routes and policies don't move.

### 1.2 Tasks

1. `web/projects/admin/src/app/layout/admin-layout.ts` — rebuild `visibleSections` as the 5 groups
   above; each group renders only if the user's roles intersect the group's union role set; each
   link inside keeps its **existing** per-feature role check (unchanged).
2. Add collapsible section headers in the sidebar template + SCSS (reuse `--surface-2`, `--line`
   tokens; logical properties).
3. Update `adminHomePath` ordering to follow the new section order:
   dashboard → orders → stock (inventory) → products → cms → users → settings.
4. i18n keys: `nav.sections.stock`, `nav.sections.content`, `nav.sections.sales`,
   `nav.sections.people`, `nav.sections.system` in `admin/src/assets/i18n/{en,ar}.json`
   (Arabic: إدارة المخزون، إدارة المحتوى، المبيعات، الأشخاص، النظام).
5. **No route path changes** — only nav grouping. No backend changes.

### Definition of Done — Phase 1
- [ ] `ng serve admin` — sidebar shows the 5 sections for `super-admin`.
- [ ] Log in as each specialist role (`content-writer`, `warehouse-keeper`, `sales`,
      `sales-manager`): only the sections containing at least one reachable link are visible; deep
      links to forbidden routes still land on `forbidden`.
- [ ] EN + AR labels render; AR is RTL-correct.
- [ ] `npm run lint` clean; no route/guard file was modified.

---

## Phase 2 — Audit log (role-based, automatic)

**Goal:** persist a tamper-evident record of every admin mutation — who, what, when, before/after —
and expose it under **System → Audit Log**.

### 2.1 Data model — `Store.Domain/AuditLog.cs`

```
AuditLog : long Id
  long?   UserId          // FK User (null for system/seeder actions)
  string  UserName        // denormalized snapshot (users can be soft-deleted)
  string  Role            // primary role claim at time of action
  string  Action          // "Create" | "Update" | "Delete" | "StockOut" | "Login" | custom
  string  EntityType      // e.g. "Product", "Order", "ContentBlock", "Stock"
  long?   EntityId
  string? EntityName      // human-readable snapshot (product name, order #)
  string? OldValuesJson   // nvarchar(max), changed properties only
  string? NewValuesJson
  string  Area            // maps to AuthPolicies area: "Catalog", "Inventory", "Sales", ...
  string? IpAddress
  string? CorrelationId   // from the existing correlation-id header/interceptor
  DateTime CreatedOn      // UTC, indexed
```

Indexes: `(CreatedOn)`, `(EntityType, EntityId)`, `(UserId)`. **No Update/Delete API ever** —
append-only.

> The existing `Activity` entity stays as-is (it is SimplCommerce heritage); `AuditLog` is a new,
> purpose-built table. Do not try to retrofit `Activity`.

### 2.2 Capture mechanism (backend)

1. `Store.Application/Services/AuditService.cs` — `IAuditService.LogAsync(AuditEntry entry)`;
   registered scoped in `AddStoreApplication`.
2. **Automatic capture:** an ASP.NET Core **action filter** `AuditActionFilter` registered globally
   for admin controllers only (apply via a `[AuditArea("Catalog")]` attribute per admin controller,
   or convention: route starts with `/api/admin`). It fires on successful POST/PUT/DELETE
   responses, reading user id/role from the JWT claims, IP from `HttpContext`, correlation id from
   the header.
3. **Value diffing** for Update actions: in `StoreDbContext`, override `SaveChangesAsync` to collect
   `ChangeTracker` entries (Added/Modified/Deleted) into a scoped `IAuditContext` that the filter
   flushes — changed properties only, excluding secrets (`PasswordHash`, `RefreshTokenHash`, tokens)
   via a hard-coded deny-list.
4. **Explicit capture** for domain events where the HTTP verb isn't descriptive enough — Phase 3's
   stock-out calls `IAuditService` directly with `Action = "StockOut"`.
5. Migration: `AddAuditLog`.

### 2.3 API — `Store.Api/Controllers/Admin/AuditLogsController.cs`

- `GET /api/admin/audit-logs` — paged; filters: date range, `userId`, `entityType`, `action`,
  `area`, free-text on `EntityName`. Gated `[Authorize(Policy = AuthPolicies.Settings)]` so only
  `super-admin`/`admin` read it (matches the existing Settings row of the access matrix).
- `GET /api/admin/audit-logs/{id}` — detail incl. old/new JSON.
- **Scoped self-view (optional, flag off by default):** if later you want managers to see their
  area's log, add `?area=` server-side enforcement per role — the plan leaves the hook in
  the service signature (`visibleAreasForUser`).

### 2.4 Admin UI — `features/system/audit-log/`

1. `data-access/admin/audit.service.ts` (httpResource for the list, following the existing admin
   service pattern; add `AdminAuditQuery` to `http-utils.ts`).
2. List page: global list classes; columns — time (local, tooltip UTC), user, role chip, action
   chip (color per action: create=green, update=gold, delete=red, stock-out=navy), entity,
   area. Filters row: date range, action, area, user search.
3. Detail drawer/dialog: side-by-side old vs new values rendered from JSON (keys translated when a
   matching i18n key exists, otherwise raw).
4. Route `system/audit-log` with `roleGuard(...AREA.Settings)`; nav link in **System** section.
5. i18n keys under `audit.*` (EN + AR — سجل التدقيق).

### Definition of Done — Phase 2
- [ ] Editing a product as `admin` produces one `AuditLog` row with only the changed properties in
      old/new JSON; `PasswordHash`-type fields never appear.
- [ ] Deleting a category, creating a coupon, and a staff login each produce rows with correct
      Action/Area.
- [ ] `content-writer` gets 403 on `/api/admin/audit-logs` and does not see the nav link.
- [ ] List filters and pagination work; detail drawer shows the diff; EN/AR + RTL OK.
- [ ] `dotnet test` green; add unit tests: diff excludes deny-listed props; append-only (no
      update/delete endpoints exist).

---

## Phase 3 — Stock management: stock-out cases, actor, and sales channel

**Goal:** every unit that leaves a warehouse is recorded with a **reason (case)**, the **person who
took it out**, an optional **sales channel**, and free-text notes — and it's queryable.

### 3.1 Enums — `Store.Domain/`

```csharp
public enum StockOutReason   // stored as int; labels via i18n
{
    Sale = 1,          // بيع
    Gift = 2,          // هدية
    Matched = 3,       // مطابقة/تسوية
    ThirdParty = 4,    // طرف ثالث
    ExternalEvent = 5, // فعالية خارجية
    Reserved = 6,      // محجوز
    DisplayOnly = 7    // للعرض فقط
}

public enum SalesChannel
{
    Showroom = 1,           // صالة العرض
    ExternalExhibition = 2, // معرض خارجي
    ExternalBroker = 3,     // وسيط خارجي
    LocalBroker = 4,        // وسيط محلي
    OnlineStore = 5         // المتجر الإلكتروني
}
```

Business rule: `SalesChannel` is **required when `Reason == Sale`**, optional (nullable) otherwise.
Validate server-side in the service, not just the UI.

### 3.2 Extend `StockHistory`

Add nullable columns (migration `AddStockOutTracking`):

```
StockOutReason? Reason
SalesChannel?   Channel
long?           PerformedById     // FK User — the person who took the product out
string?         RecipientOrRef    // broker name / event name / gift recipient (free text, 256)
string?         Note              // 1024
```

Existing rows stay null (they predate the feature). `AdjustedQuantity < 0` + `Reason != null`
identifies a tracked stock-out. Online orders that decrement stock through the existing order flow
get `Reason = Sale, Channel = OnlineStore, PerformedById = null` set automatically in the stock
service — **find where order placement decrements `Stock` in `Store.Application` and stamp these
values there**.

### 3.3 Stock-out service + API

1. `Store.Application` — extend the existing stock service with
   `StockOutAsync(StockOutRequest req, long performedByUserId)`:
   validates quantity ≤ on-hand, requires channel when reason = Sale, decrements `Stock`,
   writes `StockHistory` with all new fields, and calls `IAuditService` (`Action="StockOut"`,
   `Area="Inventory"`, entity = Product, new-values = the request payload).
2. `Store.Api/Controllers/Admin/InventoryController.cs` (existing) — add:
   - `POST /api/admin/inventory/stock-out` — body: productId, warehouseId, quantity, reason,
     channel?, recipientOrRef?, note. Policy: `AuthPolicies.Inventory`.
   - `GET /api/admin/inventory/stock-out-log` — paged `StockHistory` where `Reason != null`;
     filters: date range, reason, channel, warehouse, product search, performedBy. Include
     performer's `FullName` via join.
3. `PerformedById` defaults to the **authenticated user**; allow an optional override field
   `performedById` in the request **only for `admin`/`super-admin`** (a keeper logging on behalf of
   someone) — enforce in the service.

### 3.4 Admin UI — under **Stock management** section

1. `features/inventory/stock-out/` — "Stock Out" form page (also open as dialog from the inventory
   list row): product picker (reuse existing product search pattern from promotions/orders if one
   exists), warehouse select, quantity, **reason select (7 cases)**, **channel select (5 channels,
   shown+required only when reason = Sale, visible-optional for ExternalEvent/ThirdParty)**,
   recipient/reference, note.
2. `features/inventory/stock-out-log/` — list page: date, product, qty, reason chip, channel chip,
   performed-by, recipient/ref, note (truncate + tooltip). Filters per §3.3. Export CSV button
   (client-side from the loaded page is acceptable v1).
3. `data-access/admin/inventory.service.ts` — add the two calls + models
   (`StockOutRequest`, `StockOutLogRow`, `AdminStockOutQuery` in `http-utils.ts`).
4. i18n: `stock.reason.sale|gift|matched|thirdParty|externalEvent|reserved|displayOnly`,
   `stock.channel.showroom|externalExhibition|externalBroker|localBroker|onlineStore` (EN + AR as
   in §3.1 comments).
5. Nav: both pages under **Stock management**, `roleGuard(...AREA.Inventory)` — so
   `warehouse-keeper`, `admin`, `super-admin`.

### Definition of Done — Phase 3
- [ ] Stock-out of 2 units as `warehouse-keeper` decrements stock, writes `StockHistory` with
      reason/channel/performer, and creates an `AuditLog` row.
- [ ] Reason = Sale without channel → 400 with a translated validation message; UI enforces it too.
- [ ] Quantity > on-hand → 400.
- [ ] Placing a storefront order stamps `Sale / OnlineStore` on its stock history rows.
- [ ] Stock-out log filters by reason, channel, person, and date; shows performer name.
- [ ] Unit tests: channel-required rule, over-stock rejection, order flow stamping.
- [ ] EN/AR + RTL OK; `dotnet test` + `npm run lint` green.

---

## Phase 4 — Unique (Signature) products: first in shop, before Best Sellers, special card

**Goal:** a curated "uniqueness" flag. Flagged products (a) appear in a dedicated home rail **above**
Best Sellers, (b) sort **first** in `/shop` default ordering, (c) render with a distinct card
design. Business name suggestion: **"Signature Pieces / قِطع مميّزة"**.

### 4.1 Data model — `Store.Domain/Product.cs`

Add (migration `AddProductSignature`):

```
bool IsSignature            // default false
int  SignatureSortOrder     // default 0; lower = earlier; only meaningful when IsSignature
```

Index `(IsSignature, SignatureSortOrder)` filtered on `IsSignature = 1`.

### 4.2 Backend

1. **Admin:** `AdminProductsController` + product service — expose both fields on the product
   get/save DTOs; add `isSignature` filter to `AdminProductQuery`. Optionally a quick
   `PATCH /api/admin/products/{id}/signature` toggle for the list page. Audit-logged automatically
   via Phase 2.
2. **Storefront catalog service (`Store.Application`):**
   - Default sort of product search: `IsSignature DESC, SignatureSortOrder ASC, <existing order>`.
     Explicit user sorts (price, newest) **override** it — signature-first applies only to the
     default "relevance" ordering.
   - New endpoint `GET /api/catalog/signature?take=8` returning published, in-catalog signature
     products ordered by `SignatureSortOrder` (reuse the existing product-summary DTO so cards get
     price/thumbnail/slug for free, incl. the English overlay via `X-Culture-Id`).

### 4.3 Storefront UI

1. `features/home/` — new rail component `signature-rail` inserted in the home template **above the
   Best Sellers / featured-row rail**. Reuses the existing rail layout patterns
   (collection-rail/featured-row) but uses the new card.
2. **Special card design** — new `ui` lib component `product-card-signature` (do **not** fork the
   normal card styles inline):
   - Gold treatment from existing tokens only: border `1px solid var(--gold-bright)`, subtle
     `--accent-soft` background wash, corner ribbon/badge "Signature · مميّز" using `--accent`,
     slightly larger radius `--r-lg`, `--shadow-md` hover lift.
   - Same DOM contract (inputs) as the regular product card so `/shop` can swap card component by
     `product.isSignature`.
   - Dark mode: verify tokens flip correctly under `[data-bs-theme="dark"]`.
3. `/shop` (catalog product-list): render `product-card-signature` for items with
   `isSignature=true` (they arrive first from the API by default sort).
4. `data-access` storefront catalog service — add `signature` resource + `isSignature` on the
   product summary model.
5. i18n: `home.signature.title` ("Signature Pieces" / "قِطع مميّزة"), badge label.

### 4.4 Admin UI

- Product form (`features/products/product-form`): "Signature" toggle + sort-order number input in
  the publish/flags section; only `AREA.Catalog` roles reach it (unchanged guard).
- Products list: signature filter chip + gold badge on flagged rows.

### Definition of Done — Phase 4
- [ ] Flagging a product shows it in the home rail above Best Sellers and first in `/shop` default
      order; price-sort ignores the boost.
- [ ] Signature card is visually distinct (badge, gold border) in light + dark, EN + AR RTL.
- [ ] Unflagged behavior unchanged; SSR renders the rail (route is server-rendered).
- [ ] Toggle action appears in the audit log.
- [ ] Tests: catalog default-sort boost; signature endpoint returns only published+flagged.

---

## Phase 5 — CMS content blocks: editable static text + media, fixed design

**Goal:** business users edit the **words and images** of designed storefront sections — e.g. in
the `hero-grid` wrap: the `hero-copy` text and the `hero-media` image — without any ability to
change layout/design. Design stays in Angular templates; content comes from the DB by **key**.

### 5.1 Model — `Store.Domain/ContentBlock.cs` (migration `AddContentBlocks`)

```
ContentBlock : long Id
  string  PageKey      // "home", "about", "contact"          (64, indexed)
  string  SectionKey   // "hero-grid", "mission-band", ...    (64)
  string  BlockKey     // "hero-copy.title", "hero-copy.subtitle", "hero-copy.cta-label",
                       // "hero-media", "cta-band.title", ... (128)
  string  Type         // "text" | "richtext" | "image" | "link"
  string? Value        // AR default text (Arabic-first, like the rest of product data)
  long?   MediumId     // FK Medium when Type = image
  string? LinkUrl      // when Type = link (CTA target)
  bool    IsActive     int SortOrder    // for repeatable blocks later
  audit columns (CreatedOn/UpdatedOn)
Unique index (PageKey, SectionKey, BlockKey)
```

- **English overlay:** reuse `LocalizedContentProperty` exactly like products do (entity type
  `"ContentBlock"`, property `"Value"`), so the existing `X-Culture-Id` overlay machinery serves EN
  automatically. Do not invent a second translation mechanism.
- `richtext` v1 = a small whitelist (bold/italic/line breaks) — **sanitize server-side**
  (strip everything outside the whitelist) to keep "not design" true and prevent XSS.

### 5.2 Backend

1. **Public:** extend `ContentController` — `GET /api/content/blocks/{pageKey}` → flat list of
   active blocks for the page (value already culture-overlaid; image blocks return the
   `/user-content/...` URL). Cacheable; anonymous.
2. **Admin:** `Store.Api/Controllers/Admin/ContentBlocksController.cs`, policy
   `AuthPolicies.Content`:
   - `GET /api/admin/content-blocks?page=home` (grouped by section)
   - `PUT /api/admin/content-blocks/{id}` — **only** `Value`, `MediumId`, `LinkUrl`, `IsActive`
     editable. Keys and Type are **not** editable from the API — they are code-owned. No admin
     create/delete endpoints in v1 (blocks ship via seeder), which is what guarantees
     "text yes, design no".
   - Translations: reuse the existing localization admin endpoints/pattern for
     `LocalizedContentProperty` (find how product translations are saved and mirror it).
   - Image upload rides the existing Media area (`MediaController` + `Medium`); the block editor
     just picks/uploads a `Medium` and stores `MediumId`.
3. **Seeder:** `ContentBlockSeeder` (insert-by-key only, additive) registering the initial
   inventory for `home`: `hero-grid` → `hero-copy.title`, `hero-copy.subtitle`,
   `hero-copy.cta-label` (+ `hero-copy.cta` link), `hero-media` (image); plus one block set for
   `mission-band` and `cta-band` titles to prove the pattern. Seed AR values = the strings
   currently hard-coded in the templates; seed EN via `LocalizationSeeder` pattern.
4. All edits flow through Phase 2 audit automatically (`EntityType = "ContentBlock"`).

### 5.3 Storefront wiring

1. `data-access` storefront: `content-blocks.service.ts` — httpResource keyed by pageKey +
   language signal (re-fetches on EN/AR switch like everything else); helper
   `block(section, key): Signal<ContentBlock | undefined>`.
2. `features/home/` hero component: replace the hard-coded copy with block lookups, **with the
   current hard-coded strings as fallbacks** (`block ?? currentString`) so the page never renders
   empty if the API/seeder lags. Same for `hero-media` (fallback to the current asset), mission-band
   and cta-band titles.
3. SSR: the blocks endpoint is anonymous and same-origin — verify it resolves during server render
   (home is SSR).

### 5.4 Admin UI — **Content management → Site Content**

`features/cms/content-blocks/`:

- Page selector (home/about/contact) → sections rendered as cards; inside each card the blocks as
  labeled fields: text → input/textarea, richtext → minimal editor (or textarea v1), image →
  thumbnail + "Change image" (media picker), link → URL input.
- Per-block language tabs **AR | EN** (AR writes `Value`, EN writes the localization overlay).
- Save per section; optimistic toast (existing Toast component); "Preview" link opens the
  storefront page in a new tab.
- Route guard `roleGuard(...AREA.Content)` → `content-writer`, `admin`, `super-admin`.
- i18n keys under `cms.blocks.*`; human labels for known block keys
  (`cms.blocks.keys.hero-copy.title` = "Hero title" / "عنوان الواجهة").

### Definition of Done — Phase 5
- [ ] Editing hero title (AR + EN) as `content-writer` updates the live home page in both
      languages after refresh; switching language in the storefront swaps the text.
- [ ] Replacing `hero-media` image via the media picker updates the hero.
- [ ] API rejects attempts to change `BlockKey`/`Type`; rich text is sanitized (script tags
      stripped) — test both.
- [ ] Home renders correctly with the API down (fallback strings) and under SSR.
- [ ] Edits appear in the audit log with old/new values.
- [ ] Seeder is idempotent: two boots produce no duplicates and don't overwrite an admin edit.

---

## Phase 6 — Hardening, tests, and docs

1. **Role matrix review:** update the access-matrix table in `PROJECT-OVERVIEW.md` §4 with the new
   endpoints (audit-logs → Settings; stock-out → Inventory; content-blocks → Content;
   signature fields → Catalog). Confirm `AuthPolicies.cs` ↔ `roles.ts` parity with a checklist.
2. **Tests to add** (`tests/Store.Application.Tests`): audit diff/deny-list, stock-out rules,
   catalog signature sort, content-block sanitizer + overlay. Frontend: Vitest specs for the
   sidebar visibility logic and the block fallback helper.
3. **Migration order** for a production deploy: `AddAuditLog` → `AddStockOutTracking` →
   `AddProductSignature` → `AddContentBlocks` (they are independent, but keep this order to match
   phases). All are additive; no data backfill required.
4. **Performance:** paginate audit + stock-out logs server-side (they grow forever); consider a
   retention job later (out of scope v1).
5. **Docs:** short "How to edit site content" and "How to record a stock-out" notes for staff
   (one page each, EN/AR) in `supported-doc/`.

---

## Suggested Claude Code session prompts

```
Session 1: Read PROJECT-OVERVIEW.md and ADMIN-UPGRADE-PLAN.md. Implement Phase 1 only.
           Run npm run lint and show me the sidebar logic before finishing.
Session 2: Implement Phase 2. Write the EF migration, the action filter, and the admin page.
           Run dotnet test and add the unit tests listed in the DoD.
Session 3: Implement Phase 3. Pay attention to §3.2 — find where order placement decrements
           stock and stamp Sale/OnlineStore there.
Session 4: Implement Phase 4. The signature card must be a new ui-lib component using existing
           tokens only. Rebuild libs (npm run build:libs) before serving.
Session 5: Implement Phase 5. Reuse LocalizedContentProperty for EN — do not invent a new
           translation table. Keep hard-coded strings as fallbacks.
Session 6: Phase 6 — tests, docs, and the role-matrix update. Verify every unchecked DoD item
           from all phases.
```

> Reminders that trip people up in this repo: `npm ci --legacy-peer-deps`, `npm run build:libs`
> after touching any lib (Phase 4 touches `ui`), Node ≥ 22.22.3, and
> `appsettings.Development.json` must exist locally.
