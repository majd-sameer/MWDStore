# Backend Duplicated-Code Report

**Date:** 2026-07-14
**Scope:** `Store.Api`, `Store.Application`, `Store.Data`, `Store.Domain` (267 C# files, ~19,100 lines; `obj/`, `bin/` and EF `Migrations/` excluded)
**Method:** token-based clone detection (jscpd, min 60 tokens) — 59 raw clones, 3.34 % duplicated lines — plus manual verification and a semantic pass for duplicates a token scanner cannot see. Trivial clones (identical `using` headers) are excluded below.

---

## Summary (ranked by impact)

| # | Duplication | Occurrences | Where |
|---|-------------|-------------|-------|
| 1 | Audit-stamp overlay block on list endpoints | **22 controllers** | `Store.Api/Controllers/Admin/*` |
| 2 | Localization helpers (`WriteEnglishAsync`, `Normalize`, `EnCulture`, save→overlay→save skeleton) | 5–11 controllers | `Store.Api/Controllers/Admin/*` |
| 3 | `AdminCustomersController` ↔ `AdminUsersController` near-twins | 4 cloned blocks | admin controllers |
| 4 | Order-detail load (same `Include` chain + mapping) | 3–4 places | checkout / orders / admin orders |
| 5 | Member vs guest checkout flows | 3 cloned blocks | `CheckoutController` |
| 6 | `AdminCommentsController` ↔ `AdminReviewsController` moderation twins | 2 large blocks | admin controllers |
| 7 | Domain entities sharing identical column blocks | 5+ entities + configs | `Store.Domain`, `Store.Data/Configurations` |
| 8 | `LocalizedContentWriter` Id-keyed vs Key-keyed twins | 2×2 methods | `Store.Application/Localization` |
| 9 | Seeder "ensure English culture" + insert-count-save skeleton | 3 places | `Store.Api/Infrastructure` |
| 10 | Content-block projection (admin vs storefront) & news list overlay | 2×2 places | content controllers |
| 11 | Repeated DTO shapes (upsert requests, address models, rate DTOs) | many | `Store.Api/Models`, `Store.Application` |
| 12 | Semantic duplicates (price-calc loop, order item loops, stock skeleton) | 2× each | `Store.Application` |

---

## 1. Audit-stamp overlay block — repeated in 22 admin controllers

Every admin list action repeats this exact block (only the entity name changes):

```csharp
var ids = result.Items.Select(x => x.Id).ToList();
var stamps = await _auditStamps.ReadAsync(nameof(Comment), ids, cancellationToken);
result = result with
{
    Items = result.Items
        .Select(x => x with { CreatedBy = stamps.CreatedBy(x.Id), ModifiedBy = stamps.ModifiedBy(x.Id) })
        .ToList(),
};
```

**Found in:** all 22 files matching `_auditStamps.ReadAsync` under `Store.Api/Controllers/Admin/` (Brands, Categories, Comments, Contacts, Customers, Menus, News, Orders, Pages, Payments, ProductAttributes, ProductOptions, Products, ProductTemplates, Promotions, Reviews, Shipping, Tax ×2, Users, Vendors, Warehouses, ContentBlocks).

**Suggested fix:** one generic extension, e.g.
`Task<PagedResult<T>> WithAuditStampsAsync<T>(this PagedResult<T> result, IAuditStampReader reader, string entityType, Func<T,long> id, Func<T, AuditStamp, T> apply, CancellationToken ct)` — or constrain the DTOs to an interface with `Id`/`CreatedBy`/`ModifiedBy` and drop the two lambdas.

---

## 2. Localization helper boilerplate across admin controllers

Repeated per controller with no variation:

- `private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;` — **9 files** (Brands, Categories, Locations, Menus, News, Pages, ProductAttributes, ProductOptions, Vendors).
- `private const string EnCulture = "en-US"` (or equivalent) — **11 files**.
- `private async Task WriteEnglishAsync(long id, XUpsertRequest request, ...)` — **5 files** (Brands, Categories, Pages, Products, Vendors); each is a run of `_localizedWriter.SetAsync(EntityType, id, LocalizedProperty.X, EnCulture, request.XEn, ct)` calls, e.g. `AdminPagesController.cs:166-172` ↔ `AdminProductsController.cs:595-601`.
- The create skeleton `Add → SaveChanges → SetAsync(overlay) → SaveChanges → return DTO` recurs in every localized admin controller; inside `AdminMenusController` alone, `AddItem` vs `UpdateItem` share the tail block (`AdminMenusController.cs:160-167` ↔ `181-188`).

**Suggested fix:** move `Normalize` and the culture constant into a shared helper (e.g. `RequestCulture` already exists), and give `ILocalizedContentWriter` a bulk overload — `SetManyAsync(entityType, id, (property, value)[] , culture, ct)` — so each controller's `WriteEnglishAsync` collapses to one call with a property map.

---

## 3. `AdminCustomersController` ↔ `AdminUsersController` — near-twin controllers

Verified identical blocks (the largest clone in the codebase):

| Block | Customers | Users |
|---|---|---|
| Soft-delete (`IsDeleted`, `LockoutEnabled`, `LockoutEnd = MaxValue`, stamp, save) + `SetCustomerGroupsAsync` (byte-identical) | `AdminCustomersController.cs:149-180` | `AdminUsersController.cs:184-215` |
| `Create` flow (build `User`, `UserManager.CreateAsync`, error join, add role, set groups, save, `CreatedAtAction`) | `AdminCustomersController.cs:92-110` | `AdminUsersController.cs:109-127` |
| `AdminUserDetail`/`AdminCustomerDetail` projection (same columns) | `AdminCustomersController.cs:114-121`, `180+` | `AdminUsersController.cs:97-102`, `217-222` |

Inside `AdminUsersController` itself, `Get` (97-102) and `LoadDetailAsync` (217-222) duplicate the same projection with only the `Where` differing.

**Suggested fix:** extract a shared `UserAdminService` (or protected base controller) holding `SetCustomerGroupsAsync`, `SoftDeleteAsync`, the create flow, and a single detail projection expression; the two controllers keep only their role-filter policies.

---

## 4. Order-detail query + mapping — same `Include` chain in 3 places

```csharp
_db.Orders.AsNoTracking()
    .Include(o => o.OrderItems).ThenInclude(i => i.Product)
    .Include(o => o.ShippingAddress)
    .Include(o => o.BillingAddress)
    .FirstOrDefaultAsync(...)
```

- `CheckoutController.LoadOrderDetailAsync` — `CheckoutController.cs:231-238`
- `AdminOrdersController.Get` — `AdminOrdersController.cs:85-92`
- `OrdersController.GetById` — `OrdersController.cs:46-51` (adds the customer filter; `Track` uses a superset chain)

**Suggested fix:** a query extension in `OrderMapping` (which already owns `ToDetail()`), e.g. `IQueryable<Order> IncludeDetail(this IQueryable<Order>)`, so the chain lives once.

---

## 5. `CheckoutController` — member vs guest flows duplicated

- Shipping options: `CheckoutController.cs:59-67` ↔ `142-150` — identical except the order-amount source (`cart.Items.Sum` vs `GuestOrderAmountAsync`).
- Place order: `CheckoutController.cs:96-104` ↔ `183-191` — identical checkout construction/save; only the item source and guest-email handling differ.
- Request DTOs mirror each other too: `StorefrontModels.cs:67-79` (`ShippingOptionsRequest`) ↔ `113-125` (`GuestShippingOptionsRequest`).

**Suggested fix:** private helpers `GetShippingOptionsAsync(decimal orderAmount, AddressDto address)` and `PlaceOrderCoreAsync(IEnumerable<(long productId, int qty)> items, PlaceOrderRequest-like data, string? guestEmail)`; both endpoints become thin adapters.

---

## 6. `AdminCommentsController` ↔ `AdminReviewsController` — moderation twins

- `AdminCommentsController.cs:50-70` ↔ `AdminReviewsController.cs:51-71`: identical stamp-overlay block **plus** an identical `UpdateStatus` action (same `ValidStatuses` guard, same error text, `FindAsync`, set status, save).
- Both files also declare the same `ValidStatuses = [1, 5, 8]` moderation constants (see `ModerationStatusRequest` in `AdminModels.cs:546`).

**Suggested fix:** shared moderation constants type + a generic `SetModerationStatusAsync<TEntity>` helper (both entities expose `Status`); finding #1's extension removes the other half.

---

## 7. Domain entities sharing identical column blocks (and their EF configurations)

| Clone | Locations |
|---|---|
| SEO/meta + publish block (`Name, Slug, MetaTitle, MetaKeywords, MetaDescription, Description, IsPublished, IsDeleted…`) | `Category.cs:8-32` ↔ `Product.cs:64-90`; `Category.cs` ↔ `NewsCategory.cs:7-26`, `Page.cs:10-24`, `NewsItem.cs:14-26`; `NewsItem.cs:37-51` ↔ `Page.cs:24-38` (audit columns `CreatedOn/LatestUpdatedOn/CreatedById/LatestUpdatedById`) |
| `Address` ↔ `OrderAddress` — same 9 address columns (`Address.cs:7-26` ↔ `OrderAddress.cs:7-28`); `OrderAddress` is an intentional immutable snapshot, but the column list is maintained twice | `Store.Domain` |
| `CartRule` ↔ `CatalogRule` — same rule header (`Name, Description, IsActive, StartOn, EndOn, RuleToApply, DiscountAmount…`) (`CartRule.cs:7-20` ↔ `CatalogRule.cs:7-20`) | `Store.Domain` |
| Matching configuration clones | `AddressConfiguration.cs:17-26` ↔ `OrderAddressConfiguration.cs:17-26`; `CategoryConfiguration.cs:15-22` ↔ `PageConfiguration.cs`, `NewsCategoryConfiguration.cs`, `NewsItemConfiguration.cs`, `ProductConfiguration.cs:27-31`; `CartRuleConfiguration.cs:11-18` ↔ `CatalogRuleConfiguration.cs:11-18` |

**Suggested fix (schema-safe):** introduce interfaces (`ISeoEntity`, `IAuditedEntity`, `ISoftDeletable`) plus shared `IEntityTypeConfiguration` extension methods (`builder.ConfigureSeoColumns()`, `ConfigureAddressColumns()`). Interfaces + config helpers deduplicate the mapping **without changing table shapes**, so no new migration is required. A common abstract base class would also work but touches the model — verify with `dotnet ef migrations add` that it produces an empty migration before committing to it.

---

## 8. `LocalizedContentWriter` — Id-keyed vs Key-keyed twin methods

- `SetAsync` ↔ `SetByKeyAsync` — `LocalizedContentWriter.cs:66-85` ↔ `109-128`: identical remove-or-upsert logic; only the lookup predicate and one assigned property differ.
- `RemoveAllAsync` ↔ `RemoveAllByKeyAsync` (`141-151` ↔ `153-163`): same shape.

**Suggested fix:** one private core `SetCoreAsync(Expression<Func<LocalizedContentProperty,bool>> match, Action<LocalizedContentProperty> initKey, string? value, ...)`; the four public methods become one-liners.

---

## 9. Seeders — repeated "ensure English culture" + insert-count-save skeleton

- `ContentBlockSeeder.cs:233-253` ↔ `NewsCategorySeeder.cs:63-83`: identical `if (inserted > 0) Save` + `if (!Cultures.Any(en)) add + Save` block.
- `LocalizedContentWriter.EnsureCultureAsync` (`LocalizedContentWriter.cs:165-171`) is a third variant of the same culture-ensure logic.

**Suggested fix:** a small shared `SeederSupport.EnsureCultureAsync(StoreDbContext, string cultureId, CancellationToken)` used by both seeders (and reused by the writer).

---

## 10. Content queries duplicated between storefront and admin

- `ContentController.cs:175-184` (storefront `PageBlocks`) ↔ `ContentBlocksController.cs:48-57` (admin list): same `ContentBlocks` filter/order/projection, differing only in `IsActive` filter and two extra admin columns.
- Inside `ContentController`, the news list (`79-89`) and the alerts endpoint (`151-161`) repeat the same "project → read overlay → re-project with `overlay.Apply`" block.

**Suggested fix:** a shared block projection + a small `ApplyNewsOverlayAsync(items, ct)` helper inside `ContentController`.

---

## 11. Repeated DTO / request-model shapes (`Store.Api/Models`)

- Upsert requests with the `Name/NameEn/Slug/Description/DescriptionEn/Meta*` head: `AdminModels.cs:229-242` (Category) ↔ `258-271` (Brand) ↔ `618-631` (Vendor); also `88-98` ↔ `664-672` and `572-579` ↔ `666-673` (product vs news vs vendor meta blocks).
- Tax-rate vs table-rate DTO/request pairs: `AdminModels.cs:462-473` ↔ `499-510` and `469-477` ↔ `479-487` (same `CountryId/StateOrProvinceId/ZipCode/…` columns).
- Address shape maintained in 4 places: `AddressDto` (`StorefrontModels.cs:31-41`) ↔ `OrderAddressInfo` (`Store.Application/Orders/OrderAddressInfo.cs:8-26`) ↔ `AdminModels.cs:385-396` ↔ domain `Address`/`OrderAddress`.

**Note:** DTO-per-endpoint duplication is partly deliberate layering (API contracts should not couple to each other), so treat these as *lower priority*. Where the shapes are genuinely the same concern (the localized upsert head, the address fields), a shared base record or composition (`public AddressDto Address { get; set; }`) removes the repetition without coupling unrelated endpoints.

---

## 12. Semantic duplicates a token scanner can't see (`Store.Application`)

- **`CatalogService`** — the "map to `ProductListItem` then compute `CalculatedProductPrice` in a foreach" block appears twice: `BuildListResultAsync` and `GetSignatureProductsAsync`. Extract `ToPricedListItems(IEnumerable<Product>)`.
- **`OrderService.CreateOrderAsync`** — the master-order item loop and the vendor sub-order item loop both resolve the tax percent and strip tax from tax-inclusive prices with the same arithmetic (`productPrice /= 1 + (taxPercent / 100)`), with intentionally different price bases. Extract a `BuildOrderItemAsync(product, quantity, priceBase, ...)` helper to keep the tax math in one place.
- **`StockService`** — `UpdateStockAsync` and `StockOutAsync` share the load-product + load-stock + append-`StockHistory` + save skeleton with different validation. A private `AdjustStockCoreAsync` would keep the two audit-sensitive paths aligned.
- **`CouponService`** — the per-coupon and per-customer usage-limit guard appears in the pre-check and again inside the item loop (`CouponService.cs:56-69` vs `86-92`); a small local function would keep both checks identical.
- **`AdminShipmentsController`** — the `AdminShipmentDto` projection is written twice (`AdminShipmentsController.cs:46-51` list and `165-170` post-create reload). Extract a shared `Expression<Func<Shipment, AdminShipmentDto>>`.
- **`AdminTaxController` / `AdminShippingController`** — rate-list projections with the same `Country/StateOrProvince` name-resolution pattern (`AdminTaxController.cs:98-104` ↔ `169-175`; `AdminShippingController.cs:120-126` ↔ `193-199`).

---

## Not counted as duplication

- Identical `using` blocks at file heads (13 jscpd hits) — noise.
- `PasswordHashCompatibilityTests` legacy-hash scenarios — deliberate compatibility fixtures.
- DTO records that merely *resemble* each other across unrelated endpoints (see note in #11).

## Suggested order of attack

1. **#1 audit-stamp extension** — one small generic method deletes ~200 lines across 22 files, zero behavior risk.
2. **#2 localization helpers** — mechanical, high line count.
3. **#3 customers/users service extraction** — removes the largest single clone and keeps the two admin surfaces from drifting (the soft-delete rules already differ subtly: role guard vs self-delete guard).
4. **#4–#6, #8–#10** — small extractions, each local to one or two files.
5. **#7 domain/config interfaces** — do last; verify the EF model is unchanged (empty migration) before merging.
