# UAT Deployment with Madfoat.com Payments (IIS)

Step-by-step guide for publishing MyStore to a **UAT server behind IIS** with the MadfoatCom
(PayTabs Hosted Payment Page) gateway fully working — including the server-to-server IPN callback,
which **cannot be exercised on a developer machine** and is the main thing UAT adds.

Companion documents: topology and generic deploy procedure in `TECHNICAL-DOCUMENTATION.md` §19;
gateway internals in `Madfoat.com-steps.md`.

---

## 0. Why UAT needs special attention for payments

On localhost the integration deliberately **omits the IPN callback URL** (PayTabs rejects loopback
callbacks with `code 210`, and could never deliver to one anyway), so settlement rides on the
browser return leg alone. On UAT, with a **public HTTPS origin**, the callback is sent and PayTabs'
servers will POST signed IPNs to your API. UAT is therefore the first place the *production-shaped*
settlement path (return **and** IPN, racing, idempotent) actually runs.

Hard requirements that follow:

- The API's payment endpoints must be reachable from the public internet over **HTTPS with a valid
  certificate** (PayTabs won't deliver IPNs to self-signed endpoints).
- `Payments:PublicApiBaseUrl` must be the **public** origin, not an internal name.

---

## 1. Server prerequisites

| Component | Requirement |
|---|---|
| OS / web server | Windows Server, IIS with **URL Rewrite** and **ARR** (proxy mode enabled) |
| .NET | **.NET 10 Hosting Bundle** (ANCM) |
| Node | **≥ 22.22.3** (Angular 22 SSR server) + **NSSM** (or PM2) to run it as a service |
| SQL Server | Reachable from the API host; a `MyStore` UAT database |
| DNS + TLS | A public hostname (e.g. `uat.mystore.example`) with a valid certificate bound in IIS |
| Outbound | The API host must reach `https://madfoat-secure.paytabs.com:443` (check any egress firewall/proxy) |

Folder layout on the server: `C:\inetpub\MyStore\{api, storefront, admin\browser}`.

---

## 2. Build on the dev machine

```bash
# Backend
dotnet publish Store.Api -c Release -o publish/api

# Frontend (from web/)
npm ci --legacy-peer-deps
npm run build            # builds libs, then storefront (browser + SSR server) and admin
```

Before zipping the storefront output, remember the two baked-in values
(`TECHNICAL-DOCUMENTATION.md` §19.3):

- `environment.ts` → `ssrApiBaseUrl` must match the API's **internal** binding
  (`http://localhost:8080`) — rebuild if the port differs on UAT.
- After copying, patch `storefront/server/angular-app-engine-manifest.mjs` `allowedHosts` with the
  UAT hostname (this patch is wiped by every redeploy copy — re-apply each time).

---

## 3. Database

```powershell
# From the repo, pointing at the UAT connection string
dotnet ef database update --project Store.Data --startup-project Store.Api
```

> ⚠️ This release includes migration **`AddUserRefreshTokens`** (refresh tokens moved from the
> `User` row to a per-session `UserRefreshToken` table). Applying it **signs every existing session
> out once** — expected, announce it to UAT testers.

Reference data (identity, locations, catalog) self-seeds on first API boot; the seeders are
idempotent.

---

## 4. Deploy the API

1. Copy `publish/api` → `C:\inetpub\MyStore\api`.
2. Create `C:\inetpub\MyStore\api\appsettings.Production.json` **on the server** (never in git,
   never overwritten by a redeploy copy):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=<sql>;Database=MyStore;...;TrustServerCertificate=True"
     },
     "Jwt": { "Key": "<random, at least 32 chars>" },
     "AdminUser": { "Password": "<UAT admin password>" },
     "Payments": {
       "StorefrontBaseUrl": "https://uat.mystore.example",
       "PublicApiBaseUrl": "https://uat.mystore.example"
     }
   }
   ```

   **`Payments:PublicApiBaseUrl` is the payment-critical line.** It becomes the base of the
   `return` and `callback` URLs sent to PayTabs. It must be the public HTTPS origin (the one IIS
   serves), *not* `http://localhost:8080`. Because the public sites proxy `/api/*` to the internal
   API, the same hostname works for both values.

3. IIS site for the API bound to internal `http://localhost:8080` only (never public), app pool
   "No Managed Code", with the two mandatory `web.config` environment variables:
   `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
4. Grant the app-pool identity write ACLs on `api\App_Data`, `api\user-content`, `api\logs`.
5. Smoke test from the server: `curl http://localhost:8080/api/payments/methods` → must return
   `[{"id":"CoD",...},{"id":"MadfoatCom",...}]`.

---

## 5. Deploy storefront (SSR) and admin

Follow `TECHNICAL-DOCUMENTATION.md` §19.2 steps 3–5 unchanged:

1. Copy the whole `dist/storefront` (browser **and** server) → `C:\inetpub\MyStore\storefront`;
   run `server.mjs` as the `MyStoreSSR` Windows service (NSSM, `PORT=4000`); verify
   `curl http://localhost:4000/`.
2. Copy `dist/admin/browser` → `C:\inetpub\MyStore\admin\browser`; site root must point at the
   `browser` subfolder; SPA-fallback rewrite to `index.html`.
3. On both public sites, the rewrite `^(api|user-content)/(.*)` → `http://localhost:8080/...` comes
   **first**, with `preserveHostHeader="true"`, and the front sites inject
   `X-Forwarded-Proto: https`. There is **no CORS in production** — everything must be same-origin.

**Payment-specific check:** from an *external* machine,
`curl -X POST https://uat.mystore.example/api/payments/paytabs/callback -d '{}'` must reach the API
(expect **200** — unsigned callbacks are acknowledged-and-ignored by design; what matters is that
the route resolves publicly and does not 404/502). This is the URL PayTabs will deliver IPNs to.

---

## 6. Configure the MadfoatCom provider

1. Log in to the admin (`https://uat.mystore.example/...admin host.../`) as the UAT admin.
2. Go to **Payments → MadfoatCom** and set:

   | Field | Value |
   |---|---|
   | Enabled | ✔ |
   | Sandbox | ✔ (the crc profile is a Test-mode profile) |
   | Region | **Madfoat white-label (madfoat-secure.paytabs.com)** |
   | Profile ID | `47255` |
   | Server key | From `https://madfoat-merchant.paytabs.com` → Developers → API Keys — use the **Standard key with the Query permission** (titled *"Test API Key"*). **Not** the SDK key, and not a key with Query ✗ (pages would be created but settlement would fail with "Transaction Query not permitted with this key"). |
   | Client key | The matching client key (unused by HPP; stored for completeness) |
   | Currency | `JOD` |
   | Payment fee | `0` (or per business decision) |

3. Save. The keys land in the `PaymentProvider.AdditionalSettings` JSON in the UAT database —
   nowhere else.

Sanity probe from the API server (proves egress + key + currency in one shot):

```bash
curl -s -X POST https://madfoat-secure.paytabs.com/payment/request \
  -H "authorization: <SERVER_KEY>" -H "content-type: application/json" \
  -d '{"profile_id":47255,"tran_type":"sale","tran_class":"ecom","cart_id":"uat-probe-1",
       "cart_description":"probe","cart_currency":"JOD","cart_amount":1.000,
       "return":"https://uat.mystore.example/api/payments/paytabs/return"}'
```

A healthy reply contains `tran_ref` and `redirect_url`. (`code 1` → wrong key/host; `206` → wrong
currency; `210` → callback/return URL rejected.)

---

## 7. UAT test script

### 7.1 Happy path (approved card)

1. Storefront → add product(s) → checkout → shipping details → payment method **MadfoatCom** →
   Confirm & pay.
2. Expect a redirect to the Madfoat-branded hosted page (yellow **TEST MODE** badge, amount in
   JOD).
3. Pay with `4111 1111 1111 1111`, any future expiry, CVV `123` (fill State/Region if the page
   asks).
4. Expect: redirect back → brief "verifying" page → order confirmation; the account's order shows
   **Payment received**; the shopper is **still signed in** after the round trip.
5. Verify server-side:
   - Admin → Orders: order status *Payment received*; payment row *Succeeded* with the `TST…`
     transaction ref.
   - Merchant dashboard → Transactions: same ref, status **A**.
   - API logs: an IPN hit `/api/payments/paytabs/callback` (this is the part localhost can never
     show). No "invalid signature" warnings for this transaction.

### 7.2 Declined / abandoned payment

1. Start a payment, then either cancel on the hosted page or let it fail.
2. Expect: payment row **Failed** with the gateway's message; **order stays *Pending payment*** and
   the shopper can retry — including with a different method. A retry must succeed (unique
   `cart_id` per attempt makes this work).

### 7.3 Adversarial checks (5 minutes, worth it)

- POST garbage to `/api/payments/paytabs/callback` with a bogus `signature` header → **400**, and
  a warning in the logs. Nothing changes in the DB.
- POST `/api/payments/paytabs/verify` with `{"tranRef":"TST0000000000000"}` → **400** "Payment not
  found for this transaction."
- Call verify twice with a real settled ref → both return `approved:true`, order history shows
  **one** "payment received" entry (idempotency).

### 7.4 Bilingual

Run one payment with the storefront in Arabic — the hosted page must come up in Arabic
(`paypage_lang` follows the request culture) and the return flow must land back on the RTL UI.

---

## 8. Troubleshooting on UAT

| Symptom | Likely cause / fix |
|---|---|
| "Could not start the MadfoatCom payment…" on checkout | API log has the real PayTabs error. `code 1` → wrong key or an SDK key; `206` → currency ≠ JOD; `210` → `PublicApiBaseUrl` not public/HTTPS. |
| Payment succeeds on the hosted page but order stays *Pending payment* | Settlement failing: check for "Transaction Query not permitted with this key" (key lacks Query — swap keys in admin, then re-run verify with the same `tranRef`; the money is not lost, the transaction is queryable once the key is right). |
| No IPN ever arrives | Callback URL not publicly reachable (external `curl` test in §5), TLS invalid, or `PublicApiBaseUrl` still points at localhost — in which case the callback was omitted at page creation entirely. |
| "Invalid signature" warnings on *every* return | Server key in the DB doesn't match the key the profile signs with — both legs use the same key; re-paste it in admin. |
| Shopper logged out after returning from the gateway | Should no longer happen (per-session refresh tokens). If seen: confirm migration `AddUserRefreshTokens` is applied and requests are same-origin (`preserveHostHeader`, §19.1 gotchas). |
| Everything worked yesterday, 401s from PayTabs today | Key rotated/revoked in the merchant dashboard — keys are managed there, not in code. |

---

## 9. Going to production later

Same procedure with three substitutions: a **live** (non-Test) PayTabs profile and its Standard key
(Query enabled), `isSandbox` unchecked in the admin form, and the production hostname in
`PublicApiBaseUrl`/`StorefrontBaseUrl`. Re-run §7.3's adversarial checks — they are the ones that
guard real money.
