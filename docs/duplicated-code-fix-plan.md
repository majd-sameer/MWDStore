# Backend Duplication — Safe Fix Plan

**Date:** 2026-07-14
**Input:** `docs/duplicated-code-report.md` + backend source review
**Constraint:** every fix below is **behavior-preserving**. No route, DTO shape, status code, SQL semantics, or audit-trail behavior changes. Nothing here has been applied — this is a plan only.

## Ground rules that keep every fix safe

1. **Pure extraction only.** Each fix moves existing code into one shared place and replaces the copies with calls. The compiled behavior at every call site stays identical.
2. **Never** convert tracked writes to `ExecuteUpdateAsync`/`ExecuteDeleteAsync` — `StoreDbContext.SaveChanges` snapshots the ChangeTracker for audit logging; bulk operators bypass it.
3. **Never** merge `SaveChangesAsync` pairs where the second save consumes a generated ID from the first (order → coupon usage, entity → localized overlay, address → warehouse).
4. **No public API surface changes**: controllers keep their routes, action names, request/response records, and status codes. The Angular apps depend on them.
5. **Verification gate for every fix** (run after each one, not once at the end):
   ```
   dotnet build          # 0 warnings / 0 errors expected
   dotnet test           # all 91 tests must pass
   ```
   plus the smoke checks listed per fix.

## Priority / risk overview

| # | Fix | Risk | Effort | Lines removed (approx.) |
|---|-----|------|--------|--------------------------|
| 1 | Audit-stamp overlay extension | none — pure extraction | S | ~200 across 22 files |
| 2 | Localization helper consolidation | none | S | ~80 across 9–11 files |
| 3 | Customers/Users shared service | low — two flows converge | M | ~90 |
| 4 | Order-detail include extension | none | XS | ~20 |
| 5 | Checkout member/guest core helpers | low — same-file extraction | M | ~60 |
| 6 | Moderation status helper | none | XS | ~30 |
| 7 | Shared DTO projections (shipments, tax/shipping rates) | none | XS | ~25 |
| 8 | `LocalizedContentWriter` core method | none | S | ~50 |
| 9 | Seeder culture-ensure helper | none | XS | ~20 |
| 10 | Content overlay/projection helpers | none | S | ~30 |
| 11 | `CatalogService` priced-list helper | none | XS | ~10 |
| 12 | Domain interfaces + config helpers | **medium — do last, needs empty-migration proof** | M | ~60 |

Items 1–11 cannot change runtime behavior if done as written. Item 12 touches the EF model and carries the only real risk; it has an explicit safety proof step.

---

## Fix 1 — Audit-stamp overlay extension (22 controllers)

**New file:** `Store.Api/Infrastructure/AuditStampExtensions.cs`

```csharp
using Store.Api.Models;
using Store.Application.Auditing;

namespace Store.Api.Infrastructure;

public static class AuditStampExtensions
{
    public static async Task<PagedResult<T>> WithAuditStampsAsync<T>(
        this PagedResult<T> result,
        IAuditStampReader reader,
        string entityType,
        Func<T, long> id,
        Func<T, string?, string?, T> apply,
        CancellationToken cancellationToken)
    {
        var ids = result.Items.Select(id).ToList();
        var stamps = await reader.ReadAsync(entityType, ids, cancellationToken);
        return result with
        {
            Items = result.Items
                .Select(x => apply(x, stamps.CreatedBy(id(x)), stamps.ModifiedBy(id(x))))
                .ToList(),
        };
    }
}
```

**Each call site** (example, `AdminCommentsController.List`) shrinks from 9 lines to:

```csharp
result = await result.WithAuditStampsAsync(
    _auditStamps, nameof(Comment), x => x.Id,
    (x, c, m) => x with { CreatedBy = c, ModifiedBy = m }, cancellationToken);
```

**Why safe:** the extension body is the exact statement sequence being deleted; `ReadAsync` is still called once per list with the same ids; DTO records are re-created with the same `with` expression. Empty lists behave identically (`ReadAsync` returns `AuditStampSet.Empty` for zero ids).

**Smoke check:** open any two admin list pages (e.g. Comments, Brands) and confirm `createdBy`/`modifiedBy` still populate.

---

## Fix 2 — Localization helper consolidation

**2a. `Normalize` (9 copies) → one helper.** Add to an existing static class (suggest `Store.Api/Infrastructure/RequestCulture.cs` or a new `AdminText` class):

```csharp
public static string? NormalizeOrNull(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value;
```

Replace each private `Normalize` with a `using static` or qualified call. Identical semantics — the 9 copies are byte-identical.

**2b. `EnCulture` constants (11 copies).** Every controller already has access to `RequestCulture.EnglishCultureId` ("en-US") — the private constants duplicate it. Replace `EnCulture` usages with `RequestCulture.EnglishCultureId` and delete the constants. *Check each file first:* all current constants must equal `"en-US"` (they do — verified during the review).

**2c. Bulk overlay write.** Add one overload to `ILocalizedContentWriter` (additive — existing methods untouched, so no other caller changes):

```csharp
Task SetManyAsync(
    string entityType, long entityId, string cultureId,
    IEnumerable<(string Property, string? Value)> values,
    CancellationToken cancellationToken = default);
```

Default implementation loops over `SetAsync` — literally the same calls the five `WriteEnglishAsync` copies make today, in the same order. Each controller's `WriteEnglishAsync` becomes a single `SetManyAsync` call with its property map.

**Why safe:** additive interface member + call-order-preserving loop; staged-not-saved semantics unchanged (callers still own `SaveChangesAsync`).

**Smoke check:** edit a Brand and a Page in the admin app with English overlay values; confirm the `en` storefront shows them and blanking a field clears the overlay.

---

## Fix 3 — `AdminCustomersController` / `AdminUsersController` shared service

**New file:** `Store.Api/Infrastructure/UserAdminSupport.cs` (internal static class or a scoped service) containing the three byte-identical blocks:

```csharp
public static class UserAdminSupport
{
    /// <summary>Replaces the user's customer-group links with exactly groupIds. Does not save.</summary>
    public static async Task SetCustomerGroupsAsync(
        StoreDbContext db, long userId, IList<long> groupIds, CancellationToken ct)
    { /* body moved verbatim from either controller */ }

    /// <summary>Soft-deletes and locks the account. Saves.</summary>
    public static async Task SoftDeleteAsync(
        StoreDbContext db, User user, TimeProvider timeProvider, CancellationToken ct)
    {
        user.IsDeleted = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.LatestUpdatedOn = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }
}
```

**Deliberately NOT unified:** the *guards* around deletion differ and must stay in each controller:
- Customers: refuses when the target holds a Staff role (`user.Roles.Any(r => AppRoles.Staff.Contains(r.Role.Name!))` → `NotFound()`).
- Users: refuses self-deletion (`BadRequest("You cannot delete your own account.")`).

Only the *action after the guard* is shared. The `Create` flows can share a `BuildUser(email, fullName, phone, now)` factory; keep the differing role assignment (`AppRoles.Customer` vs request-supplied roles) at the call sites.

**Why safe:** the shared bodies are verbatim moves; the behavioral differences (guards, roles, response DTOs) remain where they are.

**Smoke check:** create + delete a customer and a staff user in the admin app; verify a staff account still can't be deleted through the customers endpoint and self-delete is still blocked.

---

## Fix 4 — Order-detail include chain

**Add to existing `Store.Api/Infrastructure/OrderMapping.cs`:**

```csharp
public static IQueryable<Order> IncludeDetail(this IQueryable<Order> orders) => orders
    .Include(o => o.OrderItems).ThenInclude(i => i.Product)
    .Include(o => o.ShippingAddress)
    .Include(o => o.BillingAddress);
```

Call sites (`CheckoutController.LoadOrderDetailAsync`, `AdminOrdersController.Get`, `OrdersController.GetById`) become `_db.Orders.AsNoTracking().IncludeDetail().FirstOrDefaultAsync(...)` with their existing predicates untouched. `OrdersController.Track` keeps its superset chain (it also includes `OrderHistories` + `AsSplitQuery`) — leave it as is, or layer `.IncludeDetail()` plus its extra include if the generated SQL is verified identical.

**Why safe:** EF composes the same include chain; the predicates and `AsNoTracking` stay at the call sites.

**Smoke check:** storefront "my orders" detail, admin order detail, and post-checkout confirmation all render items + addresses.

---

## Fix 5 — Checkout member/guest core helpers (same file only)

Inside `CheckoutController`, extract two private helpers; both endpoints keep their routes, validation, and error texts:

```csharp
private async Task<ActionResult> ShippingOptionsCoreAsync(
    decimal orderAmount, AddressDto shippingAddress, CancellationToken ct)
{
    var prices = await _shippingPriceService.GetApplicableShippingPricesAsync(new GetShippingPriceRequest
    {
        OrderAmount = orderAmount,
        ShippingAddress = shippingAddress.ToOrderAddressInfo()
    }, ct);
    return Ok(prices.Select(p => new ShippingOptionDto(p.ProviderId, p.Name, p.Price)).ToList());
}

private Checkout BuildCheckout(
    long customerId, long createdById, IEnumerable<(long ProductId, int Quantity)> items,
    string? couponCode, string? orderNote, bool isProductPriceIncludeTax, DateTimeOffset now)
{ /* the shared object-initializer block, items mapped to CheckoutItems */ }
```

The member endpoint passes `cart.Items.Sum(i => i.ProductPrice * i.Quantity)` / cart lines; the guest endpoint passes `await GuestOrderAmountAsync(...)` / posted lines and keeps its email-placeholder logic and empty-cart checks exactly where they are today.

**Why safe:** private, same-file extraction; the two flows' genuinely different parts (item source, guest email synthesis, auth) never move.

**Smoke check:** place one signed-in order and one guest order end-to-end (shipping options → place → confirmation with tracking number).

---

## Fix 6 — Moderation status helper (Comments / Reviews)

**New file:** `Store.Api/Infrastructure/Moderation.cs`

```csharp
public static class Moderation
{
    public const int Pending = 1;
    public const int Approved = 5;
    public const int NotApproved = 8;
    public static readonly int[] ValidStatuses = [Pending, Approved, NotApproved];
    public const string InvalidStatusError = "Status must be 1 (Pending), 5 (Approved) or 8 (NotApproved).";
}
```

Both controllers reference these instead of their private copies. The `UpdateStatus` action bodies stay in each controller (they differ afterward: reviews recalculate the product rating; comments don't) — only the constants and error string are shared.

**Why safe:** constants extraction only; the divergent post-update logic is untouched.

---

## Fix 7 — Shared DTO projection expressions

For projections written twice in one file, hoist each into a private static expression so EF translates the same SQL:

```csharp
// AdminShipmentsController
private static readonly Expression<Func<Shipment, AdminShipmentDto>> ToShipmentDto = s => new AdminShipmentDto(
    s.Id, s.OrderId, s.TrackingNumber, s.WarehouseId, s.Warehouse.Name, s.CreatedOn,
    s.ShipmentItems.Select(i => new AdminShipmentItemDto(
        i.Id, i.OrderItemId, i.ProductId, i.Product.Name, i.Quantity)).ToList());
```

Use `.Select(ToShipmentDto)` at both sites (`List` and the post-create reload). Same pattern for the rate projections repeated inside `AdminTaxController` and `AdminShippingController`.

**Why safe:** `IQueryable.Select` accepts the identical expression tree; generated SQL is unchanged. Verify with logging if desired (`Microsoft.EntityFrameworkCore.Database.Command` at `Information`).

---

## Fix 8 — `LocalizedContentWriter` internal core

Collapse the Id-keyed / Key-keyed twins into private cores; the four public methods keep their exact signatures (the interface is unchanged, so no caller anywhere is affected):

```csharp
private async Task SetCoreAsync(
    Expression<Func<LocalizedContentProperty, bool>> match,
    Func<LocalizedContentProperty> createRow,   // sets EntityId or EntityKey + shared fields
    string? value, string cultureId, CancellationToken ct)
{
    var row = await _db.LocalizedContentProperties.FirstOrDefaultAsync(match, ct);
    if (string.IsNullOrWhiteSpace(value))
    {
        if (row != null) _db.LocalizedContentProperties.Remove(row);
        return;
    }
    if (row == null)
    {
        await EnsureCultureAsync(cultureId, ct);
        _db.LocalizedContentProperties.Add(createRow());
    }
    else
    {
        row.Value = value;
    }
}
```

`RemoveAllAsync`/`RemoveAllByKeyAsync` similarly share a `RemoveWhereAsync(Expression<...>)` core.

**Why safe:** same statements, same order, same staged-not-saved contract; only the predicate/row-factory vary per public method — exactly the parts that differ today.

**Smoke check:** covered by Fix 2's smoke check (overlay upsert + blank-clears) plus deleting a localized entity (e.g. a menu item) and confirming its overlay rows disappear.

---

## Fix 9 — Seeder culture-ensure helper

**New file:** `Store.Api/Infrastructure/SeederSupport.cs`

```csharp
public static class SeederSupport
{
    /// <summary>Inserts the culture row if missing. Saves only when it inserted.</summary>
    public static async Task EnsureCultureAsync(StoreDbContext db, string cultureId, CancellationToken ct)
    {
        if (!await db.Cultures.AnyAsync(c => c.Id == cultureId, ct))
        {
            db.Cultures.Add(new Culture { Id = cultureId, Name = cultureId });
            await db.SaveChangesAsync(ct);
        }
    }
}
```

Used by `ContentBlockSeeder` and `NewsCategorySeeder` in place of their inline blocks. **Leave `LocalizedContentWriter.EnsureCultureAsync` alone** — it deliberately does *not* save (the caller commits atomically); merging it here would change transactional behavior.

**Why safe:** verbatim move of the two seeder blocks, which already save immediately. Seeders are idempotent and startup-only.

**Smoke check:** boot the API against the existing database (no new rows, no errors) and once against an empty database if convenient.

---

## Fix 10 — Content overlay/projection helpers

**10a.** In `ContentController`, extract the repeated news-overlay block:

```csharp
private async Task<List<T>> ApplyNewsOverlayAsync<T>(
    List<T> items, Func<T, long> id, Func<T, LocalizedOverlay, T> apply, CancellationToken ct)
{
    var cultureId = RequestCulture.OverlayCultureId(Request);
    var overlay = await _localization.GetOverlayAsync(
        LocalizedEntity.NewsItem, items.Select(id).ToList(), cultureId, ct);
    return items.Select(i => apply(i, overlay)).ToList();
}
```

The news list keeps its extra `ThumbnailUrl = _mediaUrl.GetUrl(...)` mapping inside its `apply` lambda; the alerts endpoint doesn't have one — the difference stays at the call sites.

**10b.** Storefront `PageBlocks` vs admin `ContentBlocksController.List`: the filters and projected columns genuinely differ (`IsActive` filter; admin returns `MediumId` + `IsActive`). **Recommendation: leave as-is** — forcing a shared projection would either widen the storefront payload (a response-shape change, forbidden here) or narrow the admin one. Document the pairing with a cross-reference comment instead.

---

## Fix 11 — `CatalogService` priced-list helper

```csharp
private List<ProductListItem> ToPricedListItems(IEnumerable<Product> products)
{
    var items = products.Select(ToListItem).ToList();
    foreach (var item in items)
    {
        item.CalculatedProductPrice = _pricing.CalculateProductPrice(
            item.Price, item.OldPrice, item.SpecialPrice, item.SpecialPriceStart, item.SpecialPriceEnd);
    }
    return items;
}
```

Used by `BuildListResultAsync` and `GetSignatureProductsAsync`. Verbatim move; the existing unit tests (`CatalogListingTests`, `CatalogSignatureTests`) directly cover both callers.

Related same-family items to fold in while there:
- `CouponService`: hoist the usage-limit predicate into a local function used by both the pre-check and the loop check (identical expression today).
- `OrderService`: extract the shared tax math (`taxPercent` lookup + `productPrice /= 1 + taxPercent / 100` + `TaxAmount` formula) into a private helper taking the price base as a parameter. **Keep the differing price bases** (calculated price for the master order, raw `Product.Price` for vendor sub-orders) and the sub-order's extra `ProductPrice -= TaxAmount` adjustment at the call sites — they are current business behavior, verified by `OrderTotalsTests`.
- `StockService`: optional; if extracted, the shared core must preserve each method's distinct validation, clamping, and audit fields exactly. Lowest value of the three — skip if in doubt.

---

## Fix 12 — Domain interfaces + shared EF configuration (DO LAST)

The only fix that touches the EF model. Do it in this order, and abort if step 3 fails:

1. Add **interfaces only** (no base classes) in `Store.Domain`:
   ```csharp
   public interface ISoftDeletable { bool IsDeleted { get; set; } }
   public interface IAuditedEntity
   {
       DateTimeOffset CreatedOn { get; set; }
       DateTimeOffset LatestUpdatedOn { get; set; }
   }
   public interface ISeoEntity
   {
       string? MetaTitle { get; set; }
       string? MetaKeywords { get; set; }
       string? MetaDescription { get; set; }
   }
   ```
   Entities implement them by *already having* these members — no property is added, renamed, or retyped. Interfaces are invisible to EF's relational model.
2. Add shared configuration extensions in `Store.Data/Configurations` (e.g. `builder.ConfigureAddressColumns()`) containing the duplicated Fluent API blocks, and call them from the existing `IEntityTypeConfiguration` classes. Column names/types/lengths must be copied exactly.
3. **Safety proof:** run
   ```
   dotnet ef migrations add DedupCheck --project Store.Data --startup-project Store.Api
   ```
   The generated migration must have **empty `Up`/`Down` bodies**. If it is empty, delete it (`dotnet ef migrations remove`) and commit the refactor. If it is *not* empty, the shared config diverged from an original — fix the divergence or revert; **never** apply a schema migration as part of this cleanup.
4. **Do not** introduce abstract base classes (`EntityBase` etc.) in this pass — they change CLR types EF maps and can alter conventions. Interfaces give the deduplication and compile-time consistency without that risk.
5. `Address` vs `OrderAddress` and `CartRule` vs `CatalogRule` stay separate entities/tables (intentional snapshot/product-rule split). Share only their *configuration* code and, if desired, an interface.

---

## Execution order & rollout

1. Fixes **1, 2, 6, 9** (constants + pure helpers) — one commit, near-zero risk.
2. Fixes **4, 7, 11** (query/projection extraction) — one commit; optionally compare EF's logged SQL before/after.
3. Fixes **8, 10a** (localization internals) — one commit; exercise the bilingual admin flows.
4. Fixes **3, 5** (controller flow extraction) — one commit each; run the full checkout and user-admin smoke checks.
5. Fix **12** last, gated on the empty-migration proof.

After each commit: `dotnet build` (0/0), `dotnet test` (91/91), plus that fix's smoke check. If any smoke check requires data you don't want to touch in a shared database, run against a local `MyStore` copy — all seeders are idempotent.

## Explicitly out of scope (would risk behavior)

- Adding pagination to endpoints that return full lists (response-shape change).
- Unifying `ShippingOptionsRequest` / `GuestShippingOptionsRequest` or other per-endpoint DTOs (public contract change; Angular clients bind to them).
- Base-class extraction in `Store.Domain` (model-shape risk — interfaces only, per Fix 12).
- Any `ExecuteUpdate/Delete` conversion (audit-trail loss).
- Merging `LocalizedContentWriter.EnsureCultureAsync` with the seeder version (different save semantics).
