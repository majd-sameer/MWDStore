# Catalog English Localization — Design (parked)

**Date:** 2026-06-18
**Status:** Designed, not yet implemented. Parked while UI-polish pass is done. Resume via `superpowers:writing-plans`.

## Problem

Storefront UI translates (ngx-translate, en/ar) but **product & news content is Arabic-only** (Jordan catalog seed, ~1,394 products). With English selected, chrome is English while product names/descriptions and news articles render in Arabic ("mixed language"). The DB localization table `LocalizedContentProperty` has **0 rows** and the API ignores `Accept-Language`.

Confirmed already in place: `acceptLanguageInterceptor` (core) sends `Accept-Language: <lang>` on every API call; cultures `arabic` and `en-US` are seeded; categories are already localized client-side via `categories.<slug>` ngx-translate keys + `CategoryLabelPipe`.

## Decisions (from brainstorming)

- **Translation engine:** Claude generates English in-session, in batches, output to a reviewable JSON; no external API key.
- **Scope:** Products (`Name`, `ShortDescription`, `Description`, `Specification`) for all ~1,394 products **+ News/Story** (`NewsItem`: `Name`, `ShortContent`, `FullContent`). Categories already handled. CMS pages out of scope for this round.
- **Storage:** `LocalizedContentProperty` (`EntityType`, `EntityId`, `CultureId='en-US'`, `ProperyName`, `Value`). Arabic stays as the untouched base columns.
- **Architecture:** Approach 1 — API overlay (chosen over English columns / frontend bundle).

## Design

1. **Storage.** English rows in `LocalizedContentProperty`. Arabic = base columns = fallback. Additive & reversible (delete `en-US` rows to revert).

2. **Translation production.** Read Arabic from DB in batches → generate English → write `Store.Migrator/translations.en.json` shaped `{ "Product": { "<id>": { "Name": "...", "ShortDescription": "...", "Description": "...", "Specification": "..." } }, "NewsItem": { "<id>": { "Name": "...", "ShortContent": "...", "FullContent": "..." } } }`. An idempotent loader (Store.Migrator step or guarded startup seeder) upserts into `LocalizedContentProperty`. Re-runnable.

3. **API.** Shared `ILocalizationService` (Store.Application over `StoreDbContext`): `GetOverridesAsync(entityType, ids, culture)` → batch lookup. Catalog service (product list + detail) and `ContentController` (news list + detail) overlay non-empty values when resolved culture ≠ base. Culture mapping: `Accept-Language: en` → `en-US` (overlay); `ar` → base (no overlay). Missing translation → base value (never blank).

4. **Frontend.** Already wired (`acceptLanguageInterceptor`); expect no component changes. Verify EN shows English, AR shows Arabic.

5. **Testing.** xUnit for overlay service (override wins; missing→base; ar→base). Playwright re-validation mobile EN/AR on a product + news article.

## Quick-win option (if full build deferred)

Generate translations for the **best-sellers / first-page products only** (the most-seen items) and load those, leaving the long tail to fall back to Arabic. Same architecture, smaller batch — delivers visible English on the landing/shop without translating all 1,394 up front.

## Open items

- Machine translations are best-effort; admins can edit any `LocalizedContentProperty` row later.
- Consider response caching of overrides if the per-request batch query shows up in profiling.
