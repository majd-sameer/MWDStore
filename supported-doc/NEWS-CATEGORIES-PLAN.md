# MyStore — News Categories Plan (Success Story · Activity · Alert)

> **How to use this file with Claude Code.** Keep it at the repo root next to
> `PROJECT-OVERVIEW.md`. Start the session with: *"Read `PROJECT-OVERVIEW.md` and
> `NEWS-CATEGORIES-PLAN.md`, then implement it phase by phase in order. Verify every Definition of
> Done item (build, `dotnet test`, `npm run lint`, manual smoke) before committing each phase."*
> This feature is small enough for **2–3 Claude Code sessions** (Phase 1+2, then Phase 3, then
> Phase 4).

---

## 0. Approach — reuse, don't rebuild

The codebase already has everything structural we need:

| Existing piece | Where | Role in this feature |
|---|---|---|
| `NewsItem` / `NewsCategory` entities | `Store.Domain` | The 3 categories are just **seeded `NewsCategory` rows with fixed, code-known slugs** |
| News admin CRUD | `Controllers/Admin/NewsController` (Content area) + `features/cms` news pages | Admin creates news and picks a category — **near-zero backend change** |
| Public content API | `ContentController` (`/api`) — CMS pages + news | Add category filtering + a home-alert endpoint |
| Storefront news UI | `features/content/` `news-list`, `news-detail` | Gains category tabs + category-aware card styling |
| Home rails | `features/home/` incl. **`story-rail`** | `story-rail` becomes the **Success Stories** rail; a new slim **alert band** goes at the top |
| Design tokens | `projects/ui/styles/_tokens.scss` | Alert/story/activity styling uses **existing tokens only** — no new colors |
| Localization overlay | `LocalizedContentProperty` + `X-Culture-Id` | Category names + news content stay bilingual the existing way |

**Fixed slugs (the contract between code and DB):** `success-story`, `activity`, `alert`.
Components and queries reference these slugs; the seeder guarantees they exist. Never hard-code the
numeric IDs.

**Category semantics:**

| Category | Slug | Audience purpose | Storefront surface |
|---|---|---|---|
| **Success Story** — قصة نجاح | `success-story` | Tell visitors the human story behind a product / maker | Home `story-rail` + news list tab + detail page (with optional linked product) |
| **Activity** — نشاط | `activity` | Team/program news: gallery opening, workshop, event | News list tab + detail page |
| **Alert** — تنبيه | `alert` | Important info / advertising on the **home page** | Dismissible **announcement band** at the top of home (+ appears in news list) |

**Global constraints:** bilingual EN/AR for every label and every seeded row (AR value +
`LocalizedContentProperty` EN overlay, matching the product pattern); RTL via logical properties;
seeders idempotent insert-by-slug; admin changes ride the existing Content policy
(`content-writer`, `admin`, `super-admin`) — **no role changes at all**.

---

## Phase 1 — Data & seeding (backend)

### 1.1 Inspect first

Before coding, read `Store.Domain/NewsItem.cs` and `NewsCategory.cs` and the existing admin
`NewsController` + news service to confirm field names (slug, published flag, thumbnail, category
FK — adjust the steps below to the real names rather than guessing).

### 1.2 Seed the 3 categories

Extend the existing seeding stage (add a small `NewsCategorySeeder` or extend `CatalogSeeder`'s
pattern — whichever matches how other content is seeded) to insert-by-slug:

| Slug | Name (AR, stored) | EN overlay |
|---|---|---|
| `success-story` | قصص نجاح | Success Stories |
| `activity` | أنشطة | Activities |
| `alert` | تنبيهات | Alerts |

EN names go through the `LocalizationSeeder` / `LocalizedContentProperty` pattern
(entity `"NewsCategory"`, property `"Name"`). Idempotent: two boots → no duplicates, and an admin
rename is never overwritten.

### 1.3 `NewsItem` additions (small, optional-but-recommended)

One migration `AddNewsEnhancements`, all nullable/additive:

```
long?     ProductId       // FK Product — success stories can link "the story of this product"
DateTime? AlertExpiresOn  // alerts auto-hide after this UTC time (null = no expiry)
string?   AlertCtaUrl     // optional link target for the home alert band
```

If `NewsItem` already has a link/URL field, reuse it instead of `AlertCtaUrl`.

### 1.4 Public API (`ContentController`)

1. Extend the existing news-list endpoint with `?category=<slug>` filtering (published only,
   paged, culture-overlaid — as it already does).
2. New `GET /api/home/alerts` (anonymous, cacheable ~60s): published `alert` items where
   `AlertExpiresOn` is null or in the future, newest first, `take=3`, returning id, slug, title,
   short/summary text, `AlertCtaUrl`.
3. Success-story detail: when `ProductId` is set, include a small product summary (name, slug,
   thumbnail, price) in the detail DTO so the storefront can render a "Shop this story" card —
   reuse the existing product-summary DTO.

### Definition of Done — Phase 1
- [ ] Fresh boot seeds exactly 3 categories; second boot adds nothing; renaming one in admin
      survives a restart.
- [ ] `GET /api/news?category=activity` (actual route per codebase) returns only that category,
      EN/AR overlay working via `X-Culture-Id`.
- [ ] `/api/home/alerts` excludes unpublished and expired alerts.
- [ ] `dotnet test` green; add tests: alert expiry filter, category filter.

---

## Phase 2 — Admin (easiest-path editing)

Goal: an editor opens the existing news form, picks one of the 3 categories, and gets only the
fields that category needs.

1. **News form** (`features/cms` news form): category select shows the seeded categories
   (translated). Conditional fields by selected category slug:
   - `success-story` → show **Product picker** (reuse the product-search pattern used elsewhere in
     admin, e.g. promotions/orders) → saves `ProductId`.
   - `alert` → show **Expires on** (datetime, optional) + **CTA URL** (optional) + a static hint:
     "Alerts appear on the home page" / "تظهر التنبيهات في الصفحة الرئيسية".
   - `activity` → no extra fields.
2. **News list**: add category filter chips (`.filter-chip`) for the 3 slugs + "All", and a
   category badge column.
3. `data-access/admin/cms.service.ts` + models: extend the news DTOs/queries with
   `categorySlug`, `productId`, `alertExpiresOn`, `alertCtaUrl`.
4. i18n (`admin` en/ar): `news.category.successStory|activity|alert`, `news.alert.expiresOn`,
   `news.alert.ctaUrl`, `news.story.linkedProduct`, plus the hint text.
5. No new routes, no guard changes — everything stays under the existing Content-area news pages.

### Definition of Done — Phase 2
- [ ] As `content-writer`: create one item per category; conditional fields appear/disappear with
      the category select; product picker attaches a product to a success story.
- [ ] List filters by category; badges render; EN/AR + RTL OK; `npm run lint` clean.

---

## Phase 3 — Storefront: home alert band + success-story rail

### 3.1 Home **alert band** (the new visible piece)

New storefront component `features/home/alert-band/` rendered **above the hero** (first thing in
the home template):

- **Data:** `data-access` storefront service method for `/api/home/alerts` via `httpResource`
  keyed on the language signal. Renders nothing (no layout space) when the list is empty.
- **Design — must sit inside the existing visual identity, tokens only:**
  - Slim full-width band: background `var(--accent-soft)`, `border-block-end: 1px solid
    var(--line-strong)`, text `var(--ink)`, max content width `var(--maxw)`.
  - Leading pill/badge in `var(--accent)` (gold) with an info/megaphone icon from the existing
    `ui` Icon component — gold reads as "announcement" in this palette; **do not** introduce
    alarm-red, it's outside the identity.
  - Optional CTA: existing pill Button style, green (`var(--green)`) if it's an action, or a plain
    gold text-link if informational.
  - Multiple alerts: stack up to 3, or a single band cycling on a slow timer — pick the simpler
    (stack) for v1.
  - **Dismiss (×)** per alert, persisted in `localStorage` (`dismissed_alert_<id>`), guarded for
    SSR (`typeof localStorage !== 'undefined'`; render, then hide on client hydration).
  - Dark mode: verify under `[data-bs-theme="dark"]`; RTL: logical properties, icon/dismiss flip
    naturally.
- Clicking the alert text opens the news detail (or `AlertCtaUrl` when set).

### 3.2 Success stories on home

Point the existing **`story-rail`** at real data: latest 4 published `success-story` items
(image, title, one-line excerpt, link to news detail). Keep the rail's current design untouched —
only the data source changes. If `story-rail` is currently static, add graceful fallback to its
current static content when the API returns none.

### 3.3 News list & detail (`features/content/`)

1. **news-list**: category tabs/chips (All · Success Stories · Activities · Alerts) driving the
   `?category=` query param (bind to route query params so tabs are linkable/SSR-friendly). Card
   accents per category using tokens: success-story = gold left border
   (`border-inline-start: 3px solid var(--accent)`), activity = green, alert = navy — subtle, same
   card layout.
2. **news-detail**: category badge under the title; for success stories with a linked product,
   render a "Shop this story / تسوّق هذه القصة" product card (existing product card component)
   linking to `/products/:id`.
3. i18n (`storefront` en/ar): `news.tabs.*`, `news.badge.*`, `home.alerts.dismiss`,
   `news.story.shopThisStory`.

### Definition of Done — Phase 3
- [ ] Publishing an alert makes it appear at the top of home within a minute; expiring or
      unpublishing removes it; dismiss persists across reloads; zero alerts = zero layout space.
- [ ] Home story-rail shows the latest success stories; empty state falls back cleanly.
- [ ] News tabs filter correctly and are deep-linkable; success-story detail shows the linked
      product card.
- [ ] Visual check: alert band + category accents look native to the theme in light **and** dark,
      EN **and** AR (RTL) — no hard-coded colors, tokens only.
- [ ] Home + news routes still SSR without errors (`npm run serve:ssr:storefront` smoke test).

---

## Phase 4 — Polish & tests

1. **Tests:** backend — alert expiry, category filter, story→product DTO; frontend (Vitest) —
   alert-band dismiss logic and empty-state.
2. **SEO:** news detail already SSR-managed; confirm title/meta include the item title for the new
   categories (no work expected, just verify).
3. **Docs:** add a short "Posting news & alerts" note (EN/AR) to `supported-doc/`, and update
   `PROJECT-OVERVIEW.md` §5 storefront features + API surface with the alerts endpoint.
4. **Audit:** if the Audit Log phase from `ADMIN-UPGRADE-PLAN.md` is already merged, verify news
   create/update rows appear there (should be automatic; no extra work).

### Definition of Done — Phase 4
- [ ] All tests green (`dotnet test`, `ng test`), lint clean, docs updated.

---

## Suggested Claude Code session prompts

```
Session 1: Read PROJECT-OVERVIEW.md and NEWS-CATEGORIES-PLAN.md. Implement Phases 1 and 2.
           First inspect NewsItem/NewsCategory and the existing news admin code and adapt
           field names to reality. Run dotnet test and npm run lint.
Session 2: Implement Phase 3. The alert band must use design tokens only (no new colors) and
           collapse to nothing when there are no alerts. Verify dark mode, AR/RTL, and SSR.
Session 3: Implement Phase 4 — tests and docs. Re-verify every unchecked DoD item.
```

> Repo reminders: `npm ci --legacy-peer-deps`; run `npm run build:libs` if you touch any lib
> (the alert band should live in the storefront app, not `ui`, unless you extract it — then
> rebuild libs); Node ≥ 22.22.3.
