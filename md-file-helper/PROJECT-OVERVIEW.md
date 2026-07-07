# MyStore — Project Overview & Reference

> **Purpose of this file.** A single, self-contained reference to the MyStore platform — its
> business, architecture, and code layout — written so an AI assistant (or a new developer) can
> understand and reason about the project **without reading the whole codebase**. Attach this file
> to Claude Desktop (or any assistant) and use it as the map. When you need to touch code, jump
> straight to the paths named in each section and in **§9 "Where to look"**.
>
> Companion docs: `CLAUDE.md` (dev commands), `DEPLOYMENT-RUNBOOK.md` + `instlation-Guid.md`
> (production deploy — intentionally **not** duplicated here), `supported-doc/*.md` (page specs),
> `Store.Migrator/README.md` (data migration).

---

## 1. What MyStore is

MyStore (brand name **"MadeWithDetermination — صُنع بعزيمة"**) is a full-stack e-commerce platform
derived from the open-source **SimplCommerce** project. It has two halves in one repo:

- **Backend** — ASP.NET Core Web API on **.NET 10**, clean-architecture layering, **SQL Server +
  EF Core**. Lives in `Store.Domain` / `Store.Data` / `Store.Application` / `Store.Api`.
- **Frontend** — an **Angular 22** workspace under `web/` with two apps (customer **storefront** +
  **admin** console) and four shared libraries.

The two communicate over `/api`. In development the Angular dev server proxies `/api` and
`/user-content` to the API; in production everything is served same-origin behind a reverse proxy.

---

## 2. Business domain

**MyStore is a Jordanian handicraft marketplace whose mission is to support the rehabilitation and
reintegration of inmates** — products are handmade in Jordanian correctional/rehabilitation centers,
and the store sells them under the "Made with Determination / صُنع بعزيمة" program.

| Aspect | Detail |
|---|---|
| **Market / locale** | Jordan (`JO`), 12 governorates: Amman, Irbid, Zarqa, Al-Balqa, Madaba, Mafraq, Jerash, Ajloun, Karak, Tafilah, Ma'an, Aqaba |
| **Currency** | Jordanian Dinar — **JOD (د.ا)**, 3 decimal places |
| **Languages** | Bilingual **English + Arabic**, full **RTL** for Arabic |
| **Catalog** | ~**1,391 handcrafted products** across **10 categories** |
| **Categories** | Textiles · Wooden Products · Resin Products · Earthenware & Pottery · Paint Art · Coppers · Souvenirs & Antiques · Leather Products · Packaged Products · Metal Products |
| **Shipping** | Free over **50 د.ا**, otherwise **3 د.ا** flat, to any governorate |
| **Payment** | Card / Mada (Visa/Mastercard), CliQ (bank transfer), Cash on Delivery; gateway integrations for Stripe, PayPal Express, MEPS |
| **Vendors** | Multi-vendor model — each rehabilitation center is a **Vendor**; orders can split into vendor sub-orders |
| **Checkout** | Guest checkout supported; **public order tracking** via a 6-digit tracking number (no login) |

Product data is Arabic-first (Arabic names/descriptions) with an **English overlay** fetched on
demand. Product images originate from the PSD e-shop (`e-shop.psd.gov.jo`) and can be localized to
disk via the migration tooling (§8).

---

## 3. Repository layout

```
E:\MWDStore\
├─ Store.Domain/          # Entities + ASP.NET Identity model. No project dependencies.
├─ Store.Data/            # StoreDbContext (EF Core, SQL Server), repositories, migrations.
├─ Store.Application/     # Business services, JWT, payments, seed loaders.
├─ Store.Api/             # Controllers, DI composition, auth, Swagger, startup seeders.
├─ tests/
│  └─ Store.Application.Tests/   # xUnit + EF Core InMemory
├─ Store.Migrator/        # One-off SimplCommerce→MyStore migration + catalog reset (NOT runtime)
├─ web/                   # Angular 22 workspace (storefront + admin + 4 libs)
├─ supported-doc/         # Page specs: DESIGN.md, CART-PAGE.md, PRODUCT-DETAILS-PAGE.md
├─ CLAUDE.md              # Dev setup + commands (short)
├─ DEPLOYMENT-RUNBOOK.md  # Production IIS deployment (authoritative)
├─ instlation-Guid.md     # Step-by-step install guide
└─ PROJECT-OVERVIEW.md    # ← this file
```

---

## 4. Backend architecture (.NET 10)

### Layers (one-directional; `A → B` = A references B)

```
Store.Domain      entities + Identity model, no dependencies
     ▲
Store.Data        StoreDbContext, Identity stores, repositories   →  Domain
     ▲
Store.Application services, JWT issuance, Stripe/payments, seeders →  Domain + Data
     ▲
Store.Api         controllers, DI root, JWT auth, CORS, Swagger    →  Application + Data + Domain
```

`Store.Api/Program.cs` is the composition root: registers the layers (`AddStoreData` /
`AddStoreApplication`), `AddIdentityCore<User>`, JWT bearer auth, a SPA CORS policy, and
`IMediaStorage → LocalMediaStorage`, then runs the seeders before mapping controllers.

### Domain model (grouped — see `Store.Domain/*.cs`, ~88 entity classes)

- **Users / Identity** — `User` (`IdentityUser<long>`: `FullName`, `VendorId`, refresh-token hash +
  expiry, default shipping/billing address, `Culture`, soft-delete), `Role`, `CustomerGroup`.
- **Catalog** — `Product` (price/oldPrice/specialPrice, SEO slug/meta, stock, publish flags,
  `BrandId`/`TaxClassId`/`VendorId`/`ThumbnailImageId`, audit), `Category` (tree via `ParentId`),
  `Brand`, `ProductCategory` (junction), `ProductAttribute[Group|Value]`,
  `ProductOption[Value|Combination]`, `ProductLink`, `ProductPriceHistory`.
- **Cart / Orders** — `CartItem`, `Checkout`/`CheckoutItem` (pre-order snapshot),
  `Order` (status enum, guest email, billing/shipping snapshot, totals, `TrackingNumber`,
  master/sub-order via `ParentId`), `OrderItem`, `OrderAddress`, `OrderHistory`, `Payment`.
- **Shipping / Locations** — `Country` (`JO`), `StateOrProvince` (12 governorates), `District`,
  `Address`, `Warehouse`, `Stock`, `StockHistory`, `Shipment`/`ShipmentItem`, `ShippingProvider`,
  `PriceAndDestination`.
- **Promotions / Tax** — `Coupon`, `CartRule`, `CartRuleUsage`, `CatalogRule`, `TaxClass`, `TaxRate`.
- **Content / Reviews** — `Page`, `NewsItem`/`NewsCategory`, `Review`/`Reply`, `Comment`,
  `Contact`/`ContactArea`, `Menu`/`MenuItem`, `Widget*`.
- **Media** — `Medium`, `ProductMedium` (stored on disk at `Store.Api/user-content/`, served at
  `/user-content`).
- **Vendor & misc** — `Vendor`, `WishList`/`WishListItem`, `ComparingProduct`,
  `RecentlyViewedProduct`, `ProductBackInStockSubscription`, `AppSetting`, `Activity`,
  localization (`Culture`, `LocalizedContentProperty`, `Resource`).

### API surface (`Store.Api/Controllers`)

Conventions: storefront-facing routes are `/api/<area>`; admin routes are `/api/admin/<area>`
(all admin controllers live under `Controllers/Admin/` and are gated by an **area authorization
policy** — `[Authorize(Policy = AuthPolicies.<Area>)]`, see **§4 "Roles & authorization"**).

**Public / storefront (anonymous)**

| Controller | Route | Purpose |
|---|---|---|
| `AuthController` | `/api/auth` | register, login (JWT + refresh cookie), refresh, logout, xsrf |
| `CatalogController` | `/api/catalog` | product search/filter, category products, product detail, categories, brands, vendor count |
| `ContentController` | `/api` | CMS pages, news, contact areas + submit |
| `PaymentsController` | `/api/payments` | methods, initiate (+ guest), callback, Stripe verify/webhook |
| `CheckoutController` | `/api/checkout` | shipping options + place-order (auth **and** guest variants) |
| `LocationsController` | `/api/locations` | countries / states / districts for forms |
| `ProductReviewsController` | `/api/…` | browse + create product reviews |
| `ComparisonController` / `RecentlyViewedController` | `/api/…` | compare list, recently viewed |

**Authenticated storefront (JWT bearer)**

| Controller | Route | Purpose |
|---|---|---|
| `AccountController` | `/api/account` | profile get/update |
| `CartController` | `/api/cart` | get cart + totals, add/update/remove items |
| `OrdersController` | `/api/orders` | list/detail customer orders; `GET /track?number=` is public |
| `WishlistController` | `/api/wishlist` | get, add, remove |

**Admin (`/api/admin/*`, policy-gated per area)** — 30 controllers under `Controllers/Admin/`:
Products, Categories, Brands, Orders, Payments, Inventory, Shipments, Customers, CustomerGroups,
Reviews, Dashboard, Settings, Localization, Locations, Media, Menus, Pages, News, Promotions,
Users, Vendors, Warehouses, Tax, Shipping, ProductTemplates, ProductAttributes, ProductOptions,
Comments, Contacts, SystemLogs. Each carries `[Authorize(Policy = …)]` mapping it to one of the
areas in the access matrix below.

### Roles & authorization (RBAC)

Six back-office (**staff**) roles plus the storefront **`customer`**. Role names are lowercase /
kebab-case because the exact string travels in the JWT role claim and is compared verbatim by the
Angular route guards. Defined in `Store.Api/Infrastructure/AppRoles.cs` (`Staff` = all six;
`All` = staff + customer, which the seeder ensures exist).

Each admin area is a named **authorization policy** in `Store.Api/Infrastructure/AuthPolicies.cs`,
registered in `Program.cs` via `AddAuthorization(o => o.AddStorePolicies())`. `super-admin` and
`admin` belong to every operational policy (so they keep full access); the specialist roles are
confined to their areas, and **only `super-admin` can manage users/roles**.

| Area (policy) → controllers | super-admin | admin | sales-manager | sales | warehouse-keeper | content-writer |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| **Catalog** — products, categories, brands, options, attributes, templates | ✅ | ✅ | | | | ✅ |
| **Content** — pages, menus, news · **Moderation** — reviews, comments · **Media** | ✅ | ✅ | | | | ✅ |
| **Inventory** — inventory, warehouses · **Fulfillment** — shipping, shipments | ✅ | ✅ | | | ✅ | |
| **Sales** — orders, customers, contacts, customer-groups | ✅ | ✅ | ✅ | ✅ | | |
| **Marketing** — promotions, tax · **Reports** — dashboard | ✅ | ✅ | ✅ | | | |
| **Settings** — settings, localization, locations, payments, vendors, logs | ✅ | ✅ | | | | |
| **Users** — user & role management | ✅ | | | | | |

**Frontend mirror:** `web/projects/admin/src/app/core/roles.ts` holds `STAFF_ROLES`, the per-area
role sets (`AREA.*`, which must stay in sync with `AuthPolicies`), and `adminHomeGuard`. The admin
sidebar hides links a role can't reach (`admin-layout.ts` `visibleSections`), each route adds
`canActivate: [roleGuard(...AREA.x)]`, and the login screen admits any staff role. Because roles
differ, the console has **no single landing** — `/` redirects to the role's first reachable section
(dashboard → orders → inventory → products via `adminHomePath`).

### Application services (`Store.Application`)

JWT/refresh token issuance (HMAC-SHA256) · catalog search & product detail · product pricing
(special/old price) · cart (add/update/remove + totals + coupons) · order creation from checkout +
cancel/restock + tax estimate · **gateway payments** (redirect-hosted flow for Stripe/PayPal/MEPS,
with a **sandbox mode** that simulates approval) · coupon validation · shipping rate lookup · stock
service · **localization overlay** (English translations via `LocalizedContentProperty`, requested
with an `X-Culture-Id` header).

### Data layer

`Store.Data/StoreDbContext.cs` extends `IdentityDbContext<User, Role, long, …>` with ~90 DbSets;
entity mappings live in `Store.Data/Configurations/*.cs`. SQL Server provider wired via
`AddStoreData(configuration)`. Migrations in `Store.Data/Migrations/` (initial schema, Identity,
refresh-token expiry, order tracking number, shipping-provider-on-rate).

### Startup seeders (idempotent + additive, run every boot in `Program.cs`)

`IdentitySeeder` (the 6 staff roles + `customer` via `AppRoles.All`, `guest@store.local` system
account, bootstrap admin granted **`super-admin`** so it can manage users/roles) →
`LocationSeeder` (Jordan + 12 governorates + Main Warehouse in Amman) →
`CatalogSeeder` (loads `Store.Api/catalog.seed.json`, insert-by-slug only) →
`LocalizationSeeder` (English overrides). None ever update existing rows, so admin edits survive
restarts.

### Config & auth

- **Auth model:** JWT access token returned **in the response body** (in-memory on the client);
  **rotating refresh token** in an httpOnly/Secure/SameSite=Strict cookie; **XSRF** token in a
  JS-readable cookie echoed as `X-XSRF-TOKEN` on mutations. Issuer `MyStore`, audience
  `MyStoreClients`, 60-min access expiry.
- **Dev admin login:** `admin@mystore.local` / `Admin@123` (seeded as **`super-admin`**).
- **Secrets file:** `Store.Api/appsettings.Development.json` is **git-ignored** and must exist
  locally (connection string, `Jwt:Key`, `AdminUser:Password`, `Payments:StorefrontBaseUrl`).
  Non-secret config (JWT issuer/audience, admin email) is in `appsettings.json`.

---

## 5. Frontend architecture (Angular 22, `web/`)

### Workspace (`web/angular.json`) — 2 apps + 4 libraries

| Project | Type | Port | Notes |
|---|---|---|---|
| **storefront** | app | 4200 | Customer shop; **SSR-enabled** (server bundle); SEO-managed catalog routes |
| **admin** | app | 4201 | Admin console; SPA (no SSR); dashboard charts via chart.js/ng2-charts |
| **core** | lib | — | Auth service + JWT, route guards (`authGuard`, `roleGuard`), 5 HTTP interceptors, `LanguageService`, money pipe, XSRF; wired via `provideCore({ apiBaseUrl, ssrApiBaseUrl })` |
| **data-access** | lib | — | Framework-pure models + HTTP services for storefront and admin (see below) |
| **ui** | lib | — | Reusable components (Button, Card, Accordion, Icon, Pagination, Pill, Stars, Stepper, Tag, Tile, Toast, …) + the shared style tokens |
| **util** | lib | — | Small utilities/pipes |

### Storefront features (`projects/storefront/src/app/features/`)

`home/` (hero + section rails: hero, featured-row, collection-rail, mission-band, cta-band,
trust-strip, values-row, story-rail) · `catalog/` (product-list `/shop`, product-detail
`/products/:id`, category-list `/categories`, compare `/compare`) · `cart/` (cart page + cart-drawer)
· `checkout/` (checkout, order-confirmation, payment-gateway-mock, payment-stripe-return) ·
`account/` (account hub, order-history, order-detail, wishlist, track-bar) · `auth/` (login,
register) · `content/` (CMS page, news-list, news-detail, about, contact) · `order-tracking/`
(public tracking). Catalog/content routes are server-rendered; auth/cart/checkout are client-only.

### Admin features (`projects/admin/src/app/features/`)

All below `/` require `authGuard` + `roleGuard(...STAFF_ROLES)` on the parent, then each route adds
its own `roleGuard(...AREA.x)` so a role only reaches its areas (see **§4 "Roles & authorization"**).
Folders:
`dashboard` · `products` (+ `product-form`) · `categories` · `brands` · `catalog-settings`
(product options/attributes/templates) · `orders` (list + detail) · `customers` · `users` ·
`vendors` · `inventory` · `warehouses` · `promotions` · `tax` · `shipping` · `payments`
(Stripe/PayPal/MEPS/generic provider forms) · `cms` (pages, menus, news) · `moderation` ·
`contacts` · `system` (settings, locations/country-form, localization, logs) · `auth` (login) ·
`forbidden`.

> **List-page styling note:** admin list pages have **no component SCSS** — table/filter/pager
> styling is **global** in `projects/admin/src/styles.scss` (shared classes `.list-toolbar`,
> `.search-box`, `.filter-chips`/`.filter-chip`, `.list-pager`/`.page-chip`, `.action-btn`,
> `.empty-state`). Restyle those classes to change every list at once.

### data-access services (`projects/data-access/src/lib/`)

- **Storefront:** `auth`, `account`, `cart`, `catalog`, `checkout`, `locations`, `order`,
  `payments`, `storefront-features`.
- **Admin (`admin/` subfolder):** products, categories, brands, orders, customers, users,
  inventory, product-options, product-attributes, tax, shipping, warehouses, locations, promotions,
  cms, payments, system, operations, moderation, media.
- **Pattern:** GETs use Angular's reactive **`httpResource`** (auto-refetch on signal change);
  commands (POST/PUT/DELETE) return `Observable` from `HttpClient`. Base URL, auth, language, and
  XSRF are handled by the **core** interceptors, not by each service. Models live in
  `models.ts`; query helpers in `http-utils.ts` (`API_ROOT`, `toQueryParams`, `AdminProductQuery`,
  `AdminOrderQuery`, …); `locale-state.ts` holds the reactive language signal for API culture.

### i18n

`@ngx-translate` with nested-key JSON at `projects/<app>/src/assets/i18n/{en,ar}.json`.
`LanguageService` (`projects/core/src/lib/i18n/language.service.ts`) exposes signals `lang()`
(`'en' | 'ar'`), computed `dir()`/`isRtl()`; persists an `atb_lang` cookie (1-yr, SSR-readable);
sets `<html lang>`/`<html dir>` and switches ngx-translate + `LocaleState`. RTL uses CSS **logical
properties** throughout; the `--font` token swaps to *IBM Plex Sans Arabic* in RTL.

### Design system (`projects/ui/styles/_tokens.scss` — single source of truth)

CSS custom properties on `:root`; consumed by both apps via `styles/index.scss` (imported **after**
Bootstrap so `:root` wins). **Dark mode** rides Bootstrap's `[data-bs-theme="dark"]` attribute
(tokens flip automatically).

| Group | Tokens |
|---|---|
| Accent (Antique Royal Gold) | `--accent #a7790f`, `--accent-soft #f3e7c8`, `--gold-bright #c9971e` |
| Actions (Fresh Herb Green) | `--green #5c9a3d`, `--green-strong #4c8330`, `--green-soft #eaf1e2` |
| Chrome (Royal Blue Slate) | `--navy #2e4f72`, `--navy-deep #1f3a57` |
| Canvas / surfaces (Inner Ivory) | `--canvas #fbf5e9`, `--surface #fff`, `--surface-2 #f4ebd9`, `--surface-3 #efe3cc` |
| Ink (Deep Titanium text) | `--ink #394142`, `--ink-2 #5a6364`, `--ink-3 #8a9091` |
| Lines | `--line #e6dac2`, `--line-2 #efe6d1`, `--line-strong #d8c9ab` |
| Radii | `--r 14px`, `--r-sm 10px`, `--r-lg 20px`, `--r-xl 28px` (buttons are pill) |
| Elevation / layout | `--shadow-sm/md/lg`, `--sh-green`, `--maxw 1240px` |

Built on **Bootstrap 5** + **ng-bootstrap** (Bootstrap `$primary` = green, `$dark` = titanium).

### Build gotchas

- **`dist/` path mapping:** `web/tsconfig.json` maps the lib imports (`core`, `data-access`, `ui`,
  `util`) to their **built output in `dist/`**, not source. **You must build libs before serving
  either app** — run `npm run build:libs` on a fresh tree and after changing any lib.
- **Install flag:** `npm ci --legacy-peer-deps` is **required** (ng-bootstrap@20 declares an
  Angular 21 peer vs our Angular 22).
- **Node ≥ 22.22.3** (Angular 22 CLI hard-rejects older 22.x).
- Each app proxies `/api` + `/user-content` to `https://localhost:7142` via
  `projects/<app>/proxy.conf.json`.

---

## 6. How the two halves talk

- **Dev:** Angular dev server proxies `/api` and `/user-content` → `https://localhost:7142` (the
  .NET API). **Prod:** storefront (Node SSR) + admin (static) + API all sit behind a reverse proxy,
  **same-origin**, so no CORS and XSRF works cleanly.
- **Request lifecycle (core interceptors):** base-url → auth (attach JWT) → accept-language (send
  current culture) → correlation-id → error. GET data flows through `httpResource`; the language
  signal re-triggers resources when the user switches EN/AR.
- **Payments (2-leg redirect):** SPA calls `POST /api/payments/initiate` → API returns a gateway
  redirect URL and creates a pending `Payment` → shopper pays at the gateway → gateway returns to
  the storefront → SPA settles via `POST /api/payments/stripe/verify` (or webhook) → order advances
  to `PaymentReceived`/`PaymentFailed`.

---

## 7. Local development quickstart

Requires **.NET 10 SDK**, **SQL Server** at `localhost` with a `MyStore` DB, **Node ≥ 22.22.3**, and
a local `Store.Api/appsettings.Development.json`.

```bash
# 1. API  (https://localhost:7142 + http://localhost:5094) — runs the 4 seeders on boot
dotnet run --project Store.Api --launch-profile https
#    Dev admin: admin@mystore.local / Admin@123

# 2. Frontend (from web/)
npm ci --legacy-peer-deps      # REQUIRED flag
npm run build:libs             # REQUIRED before serving on a fresh tree
ng serve storefront            # http://localhost:4200
ng serve admin                 # http://localhost:4201
```

Other commands: `dotnet build` / `dotnet test` (single test:
`dotnet test --filter "FullyQualifiedName~SomeTestName"`). Frontend: `npm run build` (libs + apps),
`npm run lint` / `lint:fix`, `ng test` (Vitest), `npm run serve:ssr:storefront`.

Backend tests live in `tests/Store.Application.Tests` (xUnit + EF InMemory): cart totals, catalog
listing/detail, checkout, inventory stock, JWT, order totals, pricing, and SimplCommerce
password-hash compatibility.

---

## 8. Migration tooling (`Store.Migrator`) — not part of the runtime

One-off scripts used to migrate data from SimplCommerce into MyStore and to reset/seed the Jordan
catalog: `00_create_copy.sql` (backup), `02_migrate.sql` (main, preserves PKs/FKs),
`10_wipe_catalog_locations.sql` (reset catalog+locations, keep identity), `11_seed_jordan.sql`
(country + 12 governorates + warehouse), `generate-catalog-seed.mjs` (build `catalog.seed.json`
from the PSD e-shop export), `20_localize_media.ps1` (download remote images into `user-content/`).
**It is destructive (delete-then-load) and is not invoked by the app — read
`Store.Migrator/README.md` before running anything.**

---

## 9. Where to look (task → files)

| Task | Start here |
|---|---|
| Add/change a **product field** | `Store.Domain/Product.cs` → `Store.Data/Configurations/` + migration → `Store.Application` (catalog/pricing) → `AdminProductsController` / `CatalogController` → `data-access` models + services → admin `product-form`, storefront `product-detail` |
| Add a **storefront API endpoint** | new/existing controller in `Store.Api/Controllers` → service in `Store.Application` → `data-access` service (httpResource/Observable) → feature component |
| Add an **admin page** | `Store.Api/Controllers/Admin/…` → `data-access/admin/…` service → new folder under `web/projects/admin/src/app/features/` + route; reuse global list classes |
| Change a **translation / label** | `web/projects/<app>/src/assets/i18n/{en,ar}.json` (and `LocalizedContentProperty` / `LocalizationSeeder` for DB content overlays) |
| Adjust a **theme color / spacing** | `web/projects/ui/styles/_tokens.scss` (tokens), `_theme.scss` (Bootstrap vars), `_components.scss` |
| Restyle **admin tables/filters/pager** | `web/projects/admin/src/styles.scss` (global `.list-toolbar`, `.table`, `.filter-chip`, `.list-pager`) |
| Touch **auth / guards / interceptors** | `web/projects/core/src/lib/` (client) · `AuthController` + `Store.Application` JWT services (server) |
| Change **roles / who can access what** | `Store.Api/Infrastructure/AppRoles.cs` + `AuthPolicies.cs` (server policies) → controller `[Authorize(Policy=…)]` → mirror in `web/projects/admin/src/app/core/roles.ts` (`AREA`/`STAFF_ROLES`) → route `roleGuard` + `admin-layout.ts` nav. Keep server ↔ client role sets in sync |
| Change **seed data** | `Store.Api/catalog.seed.json` + the seeders in `Store.Api/` (Identity/Location/Catalog/Localization) |
| Work on **payments** | `PaymentsController` + `GatewayPaymentService`/`StripeClient` (server) · storefront `checkout/payment-*` (client) |

---

## 10. Pointers (other docs)

- **`CLAUDE.md`** — concise dev setup, build/test commands, the library-build gotcha.
- **`DEPLOYMENT-RUNBOOK.md`** — authoritative production IIS deployment (server setup, deploy, test
  rounds, redeploy checklist). *Deployment details are intentionally kept there, not in this file.*
- **`instlation-Guid.md`** — step-by-step install/deploy guide.
- **`supported-doc/DESIGN.md`** — the design language / palette rationale behind the tokens.
- **`supported-doc/CART-PAGE.md`**, **`supported-doc/PRODUCT-DETAILS-PAGE.md`** — page-level specs.
- **`Store.Migrator/README.md`** — migration/reset procedure (destructive).
