# Users, Passwords & Roles

Live inventory of the accounts and roles in the **MyStore** database, generated from the running
`MyStore` DB on `DESKTOP-O20NUG8\SQLEXPRESS` on 2026-08-31.

> **On passwords:** ASP.NET Identity stores a one-way `PasswordHash` — existing passwords cannot be
> read back out of the database. The only password listed below in plaintext is the bootstrap admin's,
> and it is known solely because it is configured in `Store.Api/appsettings.Development.json`
> (`AdminUser:Password`). Every other account's password is unrecoverable; reset it instead.

---

## 1. Accounts currently in the database

| # | Email | Full name | Password | Role | Can sign in? |
|---|---|---|---|---|---|
| 2 | `admin@mystore.local` | Store Administrator | `Admin@123` | `super-admin` | Yes — admin app + storefront |
| 1 | `guest@store.local` | Guest | *unrecoverable by design* | *(none)* | **No** |

That is the complete list — **2 users, 1 role assignment**. No customer accounts exist yet; they are
created by storefront registration or from the admin back-office.

### `admin@mystore.local`

The bootstrap account, created by `IdentitySeeder` on every API startup (idempotent). The password
comes from `AdminUser:Password` in `appsettings.Development.json`, which is **git-ignored** — if that
file is missing, the seeder logs a warning and skips admin creation entirely, leaving you locked out.

The seeder also guarantees this account holds `super-admin`, because that is the only role permitted
to manage users and roles — without it nobody could ever grant the other roles. The grant is
additive: an existing admin-only account is upgraded on the next startup and keeps its other roles.

### `guest@store.local`

A **system account, not a login.** It exists to own guest (no-login) checkout orders — guest carts are
snapshotted against its user id, while the shopper's real email lives on `Order.GuestEmail` and acts
as the shared secret for the public order-tracking lookup.

It is created with a throwaway password of the form `Guest!<random-guid>A1` that is **never persisted
anywhere**, and it holds no role. Both facts are deliberate: the account cannot be signed into. Do not
try to recover this password — if you ever need to, you have a design problem, not a credentials problem.

---

## 2. The seven roles

All seven are created by `IdentitySeeder` at startup and defined in
`Store.Api/Infrastructure/AppRoles.cs`. Names are lowercase kebab-case because that exact string
travels in the JWT role claim and is compared verbatim by the Angular route guards — do not rename them.

| Id | Role | Purpose |
|---|---|---|
| 1 | `super-admin` | Unrestricted back-office access |
| 2 | `admin` | Broad back-office access, including staff user & role management |
| 3 | `sales-manager` | Sales oversight: orders, customers, vendors, promotions, payments |
| 4 | `sales` | Order processing, customer directory, vendors |
| 5 | `warehouse-keeper` | Stock: catalog, inventory, warehouses, shipping |
| 6 | `content-writer` | Content: CMS pages, news, comment/review moderation |
| 7 | `customer` | Storefront shoppers |

The first six are **staff roles** — the set allowed into the admin app at `localhost:4201`.
`customer` is storefront-only and grants no back-office access.

---

## 3. What each role can reach

Authorization is by **named policy**, not by `[Authorize(Roles = ...)]`. Each admin controller carries
`[Authorize(Policy = ...)]` for its area; the mapping lives in
`Store.Api/Infrastructure/AuthPolicies.cs`. `super-admin` and `admin` are members of every operational
policy.

| Area (policy) | Covers | super-admin | admin | sales-manager | sales | warehouse-keeper | content-writer |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `area:catalog` | Products, categories, brands, options, attributes, templates | ● | ● | | | ● | |
| `area:inventory` | Inventory, warehouses | ● | ● | | | ● | |
| `area:fulfillment` | Shipping config, shipment processing | ● | ● | | | ● | |
| `area:content` | CMS pages, menus, news | ● | ● | | | | ● |
| `area:moderation` | Reviews, comment moderation | ● | ● | | | | ● |
| `area:media` | Media library uploads | ● | ● | | | ● | ● |
| `area:sales` | Orders, customers, contacts, customer groups | ● | ● | ● | ● | | |
| `area:orders-view` | View orders and order detail | ● | ● | ● | ● | ● | |
| `area:shipments-view` | Read an order's shipment records | ● | ● | ● | ● | ● | |
| `area:vendors` | Vendor directory | ● | ● | ● | ● | | |
| `area:marketing` | Promotions | ● | ● | ● | | | |
| `area:payments` | Payment providers and configuration | ● | ● | ● | | | |
| `area:taxes` | Tax rates and tax classes | ● | ● | | | | |
| `area:reports` | Dashboard / reporting | ● | ● | | | | |
| `area:settings` | Store settings, localization, locations, logs | ● | ● | | | | |
| `area:users` | User & role management | ● | ● | | | | |
| `area:dev-assistant` | Developer Assistant portal | ● | | | | | |

`area:dev-assistant` is **super-admin only** on purpose: it exposes the complete schema and the full
route/policy topology, which is reconnaissance-grade information.

---

## 4. Signing in

```bash
curl -k -X POST https://localhost:7142/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@mystore.local","password":"Admin@123"}'
```

Returns a JWT carrying the role claim. Pass it as `Authorization: Bearer <token>` on admin endpoints.
Token lifetime is 60 minutes (`Jwt:ExpiryMinutes` in `appsettings.json`).

- Admin app: <http://localhost:4201>
- Storefront: <http://localhost:4200>

---

## 5. Adding accounts

- **Staff** — from the admin app under user & role management (`area:users`; requires `super-admin`
  or `admin`).
- **Customers via admin** — `POST /api/admin/customers` creates the user and explicitly assigns the
  `customer` role.
- **Customers via storefront** — `POST /api/auth/register` creates the user but **assigns no role at
  all**. Self-registered shoppers therefore end up role-less rather than in `customer`. This is worth
  knowing before you write any logic that assumes shoppers carry the `customer` role; it does not
  affect back-office security, since role-less accounts fail every admin policy.

---

## 6. Two things to be careful about

**The password policy is wide open in this build.** `Store.Api/Program.cs` sets
`RequiredLength = 4` with `RequireDigit`, `RequireNonAlphanumeric`, `RequireUppercase`,
`RequireLowercase` all `false` and `RequiredUniqueChars = 0`. Fine for local development; tighten it
before anything faces the internet.

**This file contains a plaintext password.** It documents a local development credential only, but
`docs/` is committed to git — unlike `appsettings.Development.json`, which is git-ignored precisely to
keep this value out of the repository. Either change the admin password on any deployed environment,
or add this file to `.gitignore` before committing:

```
docs/USERS-AND-ROLES.md
```
