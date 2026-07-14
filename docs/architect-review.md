# Architect Review — MyStore Backend (C# / EF Core)

**Date:** 2026-07-14
**Scope:** `Store.Domain`, `Store.Data`, `Store.Application`, `Store.Api` (332 C# files)
**Companion docs:** `docs/duplicated-code-report.md` (findings), `docs/duplicated-code-fix-plan.md` (the behavior-preserving plan — **now fully applied**, all 12 fixes)
**Verification:** `dotnet build` 0 warnings/0 errors and `dotnet test` 91/91 after *every* batch; EF model proven unchanged by an empty `DedupCheck` migration; live API boot + storefront smoke checks (news/alerts/content-blocks return 200, English overlay applies).

---

## 1. Critical Analysis

### AI Smells & Overhead

- ✅ **No generic repository wrappers** — services and controllers use `StoreDbContext` directly with purposeful, per-query LINQ. Nothing to remove here; do not add one.
- ✅ **Modern C# already in place** — file-scoped namespaces, NRT (`<Nullable>enable</Nullable>`) in every project, collection expressions, records for DTOs.
- ❌ **Copy-paste boilerplate at scale** *(fixed)* — the real "generated code" smell was repetition, not abstraction:
  - the audit-stamp overlay block cloned across **22 admin controllers** (~200 lines);
  - 9 byte-identical private `Normalize` methods and 10 per-file `EnCulture` aliases of a constant that already existed (`RequestCulture.EnglishCultureId`);
  - 5 `WriteEnglishAsync` methods that were just runs of `SetAsync` calls;
  - moderation constants (`[1, 5, 8]` + error string) duplicated in Comments/Reviews controllers.
- ⚠️ **Primary constructors not used** — the codebase consistently uses classic constructor DI. Left as-is deliberately: churning ~50 files for syntax alone adds review noise with zero behavioral value; adopt for *new* classes.

### OOP & Redundancy (DRY)

All items below were duplicated logic that could silently drift; each is now defined once *(applied, behavior-preserving)*:

- `AuditStampExtensions.WithAuditStampsAsync` (`PagedResult<T>` + `IReadOnlyList<T>` overloads) replaces 17 clone sites. Six controllers that already fold stamps into a single-pass projection (Brands, Categories, Vendors, ProductOptions, ProductAttributes, ContentBlocks) were **left alone** — they aren't the cloned block, and forcing them through the helper would add a pass, not remove one.
- `ILocalizedContentWriter.SetManyAsync` — additive overload; each controller's English-overlay write is now one call with a property map.
- `LocalizedContentWriter` — id-keyed/key-keyed twin methods collapsed into `SetCoreAsync`/`RemoveWhereAsync`; the four public methods (and their staged-not-saved contract) are unchanged.
- `UserAdminSupport` (`BuildUser`, `SetCustomerGroupsAsync`, `SoftDeleteAsync`) — removes the largest clone (customers ↔ users admin). **The guards stayed in the controllers**: the customer screen still refuses to touch staff accounts, the user screen still refuses self-deletion. Only the action behind the guard is shared.
- `CheckoutController` — member/guest flows now share `ShippingOptionsCoreAsync` and `BuildCheckout`; the genuinely different parts (cart source, guest-email synthesis, auth) never moved.
- `OrderMapping.IncludeDetail()` — the order-detail include chain, defined once, used by 4 call sites (`Track` keeps its superset chain with `AsSplitQuery`).
- Shared `Expression<Func<...>>` projections in `AdminShipmentsController`, `AdminTaxController`, `AdminShippingController` — same expression tree, so EF emits identical SQL for list and post-create reload.
- `OrderService.BuildOrderItemAsync` — the tax-strip arithmetic exists once; the **intentionally different price bases** (calculated price for the master order, raw `Product.Price` for vendor sub-orders) and the sub-order's `ProductPrice -= TaxAmount` adjustment stay at the call sites, protected by `OrderTotalsTests`.
- `CatalogService.ToPricedListItems`, `CouponService` usage-limit local functions, `SeederSupport.EnsureCultureAsync`, `ContentController.ApplyNewsOverlayAsync` — small single-definition helpers for the remaining clones.
- **Domain/EF config:** `ISeoEntity`, `ISoftDeletable`, `IAuditedEntity` marker-style interfaces on `Category`, `Product`, `Page`, `NewsItem`, `NewsCategory`, plus `ConfigureSeoColumns` / `ConfigureAddressColumns` / `ConfigureRuleColumns` extensions shared by the 9 configurations that maintained identical Fluent blocks. **Proof:** `dotnet ef migrations add DedupCheck` produced empty `Up`/`Down` — the relational model is byte-identical.
- **`StockService` skeleton left duplicated on purpose** — its two paths carry distinct validation, clamping, and audit fields; merging them risks aligning things that must stay different (lowest value, highest subtlety — as the plan recommended).

### Domain Encapsulation

- ❌ **The domain is anemic** — public `{ get; set; }` everywhere, no invariants, no behavioral methods. This is the one review axis where the code diverges most from "rich domain model" doctrine, and it was **deliberately not "fixed"**:
  - These entities are a data-fidelity port of SimplCommerce; every write path (controllers, services, seeders, the migrator) uses object initializers and property mutation. Converting to private setters + `UpdateDetails()`-style methods is a rewrite of every write path in the system — precisely the "behavior-changing refactor" the project's own fix plan forbids, with no test safety net at the API layer.
  - `StoreDbContext.SaveChanges` derives the **audit trail from ChangeTracker snapshots**; wholesale constructor/mutator changes would need re-verification of audit capture for every entity.
  - **Recommendation, not a change:** hold *new* aggregates to the richer standard (structured constructors, private setters, EF's private parameterless constructor), and harden hot aggregates (Order, Checkout) opportunistically when real behavior changes touch them.
- ✅ **Base classes rejected in favor of interfaces** — an `EntityBase`/`AuditableEntity` hierarchy changes the CLR types EF maps and can shift conventions. Interfaces give compile-time consistency with zero model risk, and the empty-migration proof confirms it.

### Security & Isolation

- ✅ **SQL injection:** zero raw SQL in the entire backend — no `FromSqlRaw`, `ExecuteSqlRaw`, or interpolated SQL anywhere; every query is LINQ and fully parameterized (verified again in boot logs: `Parameters=[@normalizedName='?' …]`).
- ✅ **Sensitive data logging:** `EnableSensitiveDataLogging` appears nowhere; parameter values are masked in logs by default. Nothing to wrap in an environment check because it is never enabled.
- ✅ **Secrets:** connection string, JWT key, and dev admin password live in git-ignored `appsettings.Development.json`.
- ✅ **Layer purity:** `Store.Domain` has **no EF Core dependency** (only `Microsoft.Extensions.Identity.Stores` for the Identity model). Controllers never return entities — every endpoint projects to DTO records in `Store.Api/Models`.
- ✅ **Audit integrity:** no `ExecuteUpdate`/`ExecuteDelete` anywhere — all writes flow through `SaveChanges` so the audit snapshot sees them. This is a **standing constraint**: never introduce the bulk operators here.
- ⚠️ **Global query filters intentionally absent.** review.md asks for `HasQueryFilter(e => !e.IsDeleted)`; retrofitting it now would silently change the SQL of *every* query, break the admin `includeDeleted` toggles (they'd need `IgnoreQueryFilters()` edits), and alter storefront behavior. Soft-delete scoping is explicit per query today and that is documented on `ISoftDeletable`. If filters are ever adopted, do it as its own change with endpoint-level verification — not as cleanup.

---

## 2. Refactored Codebase

All changes are applied to the working tree (48 files, +484/−625 net −141 lines). New shared components:

| File | Provides |
|---|---|
| `Store.Api/Infrastructure/AuditStampExtensions.cs` | `WithAuditStampsAsync` over `PagedResult<T>` / `IReadOnlyList<T>` |
| `Store.Api/Infrastructure/AdminText.cs` | `NormalizeOrNull` (blank → null, matches overlay-writer semantics) |
| `Store.Api/Infrastructure/Moderation.cs` | `Pending/Approved/NotApproved`, `ValidStatuses`, error text |
| `Store.Api/Infrastructure/SeederSupport.cs` | seeder-only `EnsureCultureAsync` (saves; the writer's non-saving variant kept separate on purpose) |
| `Store.Api/Infrastructure/UserAdminSupport.cs` | `BuildUser`, `SetCustomerGroupsAsync`, `SoftDeleteAsync` |
| `Store.Domain/ISeoEntity.cs`, `ISoftDeletable.cs`, `IAuditedEntity.cs` | model-invisible domain interfaces |
| `Store.Data/Configurations/ConfigurationExtensions.cs` | `ConfigureSeoColumns`, `ConfigureAddressColumns`, `ConfigureRuleColumns` |

Representative call-site shape (was 9 lines per controller, ×22):

```csharp
return Ok(await result.WithAuditStampsAsync(
    _auditStamps, nameof(Comment), x => x.Id,
    (x, createdBy, modifiedBy) => x with { CreatedBy = createdBy, ModifiedBy = modifiedBy },
    cancellationToken));
```

Plus targeted extractions inside existing files: `OrderMapping.IncludeDetail`, `LocalizedContentWriter.SetCoreAsync`/`RemoveWhereAsync`/`SetManyAsync`, `OrderService.BuildOrderItemAsync`, `CatalogService.ToPricedListItems`, `CouponService` limit local functions, `CheckoutController.ShippingOptionsCoreAsync`/`BuildCheckout`, `ContentController.ApplyNewsOverlayAsync`, shared DTO projection expressions in the shipments/tax/shipping admin controllers.

**Not changed (by design):** routes, DTO shapes, status codes, error texts, save ordering (every ID-dependent `SaveChangesAsync` pair untouched), the `Track` endpoint's split query, `PageBlocks` vs admin content-blocks projections (different payloads are contract, not duplication), `StockService`, and all public DTO records the Angular apps bind to.

---

## 3. Humanization Justification

- **Deduplicated where drift hurts, kept apart what must differ.** A generator flattens everything that looks similar; an engineer asks *why* two blocks are similar. The customers/users guards, the two order-item price bases, the saving vs non-saving culture-ensure variants, and the storefront vs admin content projections all encode business rules — they stayed separate, and the shared helpers' XML docs say so ("Does not save.", "Callers choose the price base…").
- **Every risky claim is proven, not asserted.** The EF-model refactor is gated on an empty generated migration; every batch passed build + full test suite; the running API was exercised end-to-end (localized news list verified through the new overlay helper). The one piece of tooling noise this produced (snapshot regeneration churn) was recognized as cosmetic and reverted rather than committed.
- **Idiom over dogma.** review.md's checklist was applied with judgment: interfaces instead of base classes (model safety), no global query filters (semantic safety), no primary-constructor churn (review-noise economy), no rich-domain rewrite bolted onto a migration-faithful codebase. Where the checklist and the system's real constraints conflicted, the constraint won and the reasoning is written down.
- **The security posture was verified, not decorated.** No raw SQL exists to parameterize; sensitive logging is off by default and confirmed in live logs; the audit pipeline's dependency on tracked writes is now an explicit, documented invariant (`ExecuteUpdate/Delete` forbidden) rather than tribal knowledge.

### Suggested commit grouping (nothing committed yet)

1. fixes 1, 2, 6, 9 — shared helpers/constants (`Store.Api/Infrastructure` + controllers)
2. fixes 4, 7, 11 — query/projection extraction (`OrderMapping`, shipments/tax/shipping, services)
3. fixes 8, 10a — localization internals
4. fixes 3, 5 — customers/users + checkout flow extraction
5. fix 12 — domain interfaces + shared EF config (empty-migration proof noted in the message)
