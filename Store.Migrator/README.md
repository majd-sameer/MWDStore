# Store.Migrator — SimplCommerce → MyStore data migration

One-off migration on `MSALEH\SQL` copying **SimplCommerce** (source) into **MyStore** (target).

## Why SQL scripts (not a C# console app)
The two schemas are **column-identical**; only the table names differ — MyStore strips the
SimplCommerce module prefix (`Core_User` → `User`, `Catalog_Product` → `Product`,
`Orders_Order` → `Order`). Column diff across all 85 tables found a single difference:
`User.RefreshTokenExpiresAt` is new in MyStore, nullable, left NULL. That makes a set-based
`INSERT … SELECT` with `IDENTITY_INSERT` the right tool: it preserves primary keys (so every
foreign key still lines up) and copies ASP.NET Identity `PasswordHash` / `SecurityStamp`
verbatim, so **old logins keep working**. An EF/console approach would re-key rows and fight
identity inserts for no benefit.

## Ordering / referential integrity
There is a genuine FK **cycle** (`User.DefaultShippingAddressId ↔ UserAddress.UserId`) plus
self-referencing `ParentId` trees (Category, Order, Comment, MenuItem), so no single linear
insert order satisfies every FK. Instead the script:
1. disables all FK constraints in the target,
2. clears + loads every table (PKs preserved),
3. re-enables every FK `WITH CHECK CHECK` to **validate** referential integrity (reports, does
   not abort, on any violation),
4. reconciles source vs target row counts.

## Files
- `00_create_copy.sql` — `COPY_ONLY` backup of MyStore → restore as `MyStore_MigrationTest`.
  Also leaves `…\MSSQL\Backup\MyStore_migtest.bak` as a pre-migration rollback point for MyStore.
- `02_migrate.sql` — the migration. Target DB is parameterised via `-v TargetDb=`.
- `10_wipe_catalog_locations.sql` — wipes everything **except identity** (`User`, `Role*`, `User*`,
  `AppSetting`) and the lookups `Culture` / `EntityType` / `ActivityType`; reseeds identity columns
  to start at 1; validates FKs. Target DB parameterised via `-v TargetDb=`.
- `11_seed_jordan.sql` — Country `JO`, the 12 governorates (ISO 3166-2:JO codes, `Type='Governorate'`),
  and a "Main Warehouse" in Amman. Idempotent.
- `generate-catalog-seed.mjs` — `supported-doc/psd_eshop_products.csv` → `Store.Api/catalog.seed.json`
  (consumed by `CatalogSeeder` at API startup).
- `20_localize_media.ps1` — downloads every `Media` row whose `FileName` is still an external URL
  into `Store.Api/user-content/` (as `m{MediaId}{ext}`, git-ignored) and repoints the row at the
  local file. Idempotent; failures keep the external URL and are retried on the next run.

## Run
```bat
set S=MSALEH\SQL
sqlcmd -S %S% -U sa -P *** -C -b -i 00_create_copy.sql
:: dry run against the copy, review the reconciliation + RI report
sqlcmd -S %S% -U sa -P *** -C -W -s"|" -v TargetDb="MyStore_MigrationTest" -i 02_migrate.sql
:: for real
sqlcmd -S %S% -U sa -P *** -C -W -s"|" -v TargetDb="MyStore" -i 02_migrate.sql
```
`SET QUOTED_IDENTIFIER ON` is set at the top because the Identity `User`/`Role` tables carry
filtered unique indexes (`UserNameIndex`), which require it for any DML — sqlcmd defaults it off.

## Jordan catalog reset (executed 2026-06-11)
Per `supported-doc/catalog-jordan-reset-plan.md`: fresh `00_create_copy.sql` backup, then `10` + `11`
dry-run against `MyStore_MigrationTest` (74 tables wiped, 0 RI violations) and run for real against
`MyStore`. API boot re-seeded the catalog from `catalog.seed.json`: 10 categories, 1,391 products,
all with Jordan-warehouse `Stock` rows and thumbnails. `DevDataSeeder` (US sample data) was unwired
from `Program.cs` at the same time. Shipping/payment providers self-seed on first admin visit
(Free Shipping + Cash On Delivery enabled by default). Identity untouched: 25 users, 4 roles —
`admin@mystore.local` still signs in.

## Result (executed 2026-06-10)
All 85 tables reconciled source = target; 0 RI violations; 0 untrusted FKs. Live MyStore now
holds 23 users (hashes identical), 4 roles, 14 products and the full catalog tree. SimplCommerce
has no orders/carts/checkouts, so that tier is empty by design. The migration is **destructive**
(delete-then-load): MyStore's prior dev-seed rows, incl. the `admin@mystore.local` seed login,
were replaced — sign in with the SimplCommerce credentials (`admin@simplcommerce.com`, …).
