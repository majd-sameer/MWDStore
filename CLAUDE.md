# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**MyStore** — a full e-commerce platform derived from the open-source **SimplCommerce** project.
Two halves living in one repo:

- **Backend** — ASP.NET Core Web API on **.NET 10**, clean-architecture layering, SQL Server + EF Core.
- **Frontend** — an **Angular 22** workspace under `web/` with two apps (customer storefront + admin) and four shared libraries.

The two communicate over `/api` (the Angular dev server proxies `/api` and `/user-content` to the API).

## Running the full stack (development)

Requires: .NET 10 SDK, SQL Server reachable at `localhost` with a `MyStore` database, and **Node ≥ 22.22.3** (Angular 22 CLI hard-requires this — older 22.x is rejected).

```bash
# 1. API  (https://localhost:7142  +  http://localhost:5094)
dotnet run --project Store.Api --launch-profile https
#    On boot it runs IdentitySeeder, LocationSeeder, CatalogSeeder (all idempotent).
#    Dev admin login: admin@mystore.local / Admin@123

# 2. Frontend  (from web/)
npm ci --legacy-peer-deps      # REQUIRED flag — ng-bootstrap@20 declares an Angular 21 peer vs our Angular 22
npm run build:libs             # REQUIRED before serving apps on a fresh tree — see "library build" gotcha
ng serve storefront            # http://localhost:4200  (proxies /api -> https://localhost:7142)
ng serve admin                 # http://localhost:4201
```

## Common commands

Backend (run from repo root):
- Build: `dotnet build`
- Test: `dotnet test` (xUnit + EF Core InMemory, project `tests/Store.Application.Tests`)
- Single test: `dotnet test --filter "FullyQualifiedName~SomeTestName"`

Frontend (run from `web/`):
- Build everything: `npm run build` (builds libs then apps)
- Build only the libs: `npm run build:libs` — `ng build data-access && ng build util && ng build ui && ng build core`
- Lint: `npm run lint` / autofix: `npm run lint:fix` (a `prebuild` hook also lints)
- Unit tests: `ng test` (Vitest)
- SSR production serve of storefront: `npm run serve:ssr:storefront` (runs `dist/storefront/server/server.mjs`)

## Backend architecture

One-directional layer dependencies (`A -> B` = A references B):

- **Store.Domain** — entities + ASP.NET Identity model. No project dependencies.
- **Store.Data** — `StoreDbContext` (EF Core, SQL Server), Identity stores, repositories. Wired via `AddStoreData(configuration)`. References Domain.
- **Store.Application** — services / business logic, JWT issuance, Stripe payments. Wired via `AddStoreApplication()`. References Domain **and** Data.
- **Store.Api** — controllers, DI composition, JWT bearer auth, CORS, Swagger, startup seeders. References Application, Data, Domain.

`Store.Api/Program.cs` is the composition root: it registers the layers (`AddStoreData` / `AddStoreApplication`), `AddIdentityCore<User>`, JWT bearer authentication, a SPA CORS policy, and `IMediaStorage -> LocalMediaStorage`, then runs the three seeders before mapping controllers.

Controller route conventions (see `Store.Api/Controllers`):
- Storefront-facing: `/api/catalog`, `/api/cart`, `/api/checkout`, `/api/orders`, `/api/account`, `/api/auth`, `/api/wishlist`, `/api/comparison`, `/api/locations`, `/api/payments`.
- Admin-facing: everything under `/api/admin/*` (products, categories, orders, customers, settings, media, …).

Data & config notes:
- The connection string, JWT signing key, and dev admin password live in `Store.Api/appsettings.Development.json`, which is **git-ignored** — it must exist locally for the API to run. Default connection uses SQL Server Integrated Security against `localhost`/`MyStore`.
- Uploaded media is stored on disk under `Store.Api/user-content/` (git-ignored) and served at `/user-content`.
- The catalog is seeded from `Store.Api/catalog.seed.json` at startup.

## Frontend architecture

Single Angular CLI workspace in `web/` (`angular.json`), six projects:

- Apps: **storefront** (`projects/storefront`, port 4200, SSR-enabled) and **admin** (`projects/admin`, port 4201).
- Libraries: **core**, **data-access**, **ui**, **util** (`projects/*`).

**Library build gotcha:** `web/tsconfig.json` maps the lib imports (`core`, `data-access`, `ui`, `util`) to their **built output in `dist/`**, not to source. So the libraries must be built (`npm run build:libs`) before either app can compile or serve. The committed `dist/` may be stale — rebuild libs after changing any lib.

Other frontend specifics: ng-bootstrap + Bootstrap 5 for UI, chart.js / ng2-charts in the admin dashboard, `@ngx-translate` for i18n (English + Arabic, including RTL). Both apps proxy `/api` and `/user-content` to `https://localhost:7142` via `projects/<app>/proxy.conf.json`.

## Store.Migrator (one-off, not part of the runtime)

`Store.Migrator/` holds the SQL scripts and generators used to migrate data from SimplCommerce into MyStore and to reset/seed the catalog (`02_migrate.sql`, `10_wipe_catalog_locations.sql`, `11_seed_jordan.sql`, `generate-catalog-seed.mjs`, `20_localize_media.ps1`). It is **not** in the solution and is not invoked by the app — read `Store.Migrator/README.md` before running anything there; the migration is destructive (delete-then-load).

## Deployment

Production runs behind IIS with URL Rewrite + ARR reverse-proxying: the storefront is served by a **Node SSR service** (`server.mjs`, port 4000), the admin SPA is served statically, and `/api` + `/user-content` proxy to the .NET API. Full procedure and troubleshooting are in `DEPLOYMENT-RUNBOOK.md` and `instlation-Guid.md`.
