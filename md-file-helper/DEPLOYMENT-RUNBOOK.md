# MyStore — Full Deployment Runbook (IIS + external SQL Server)

This is a do-this-then-this runbook for putting MyStore on a Windows + IIS server, with the
database living on a **separate** SQL Server. It is organised in the execution order you actually
need:

1. **Part 1 — Server setup** (one-time prep of the Windows/IIS box)
2. **Part 2 — Deploy MyStore** (database → backend → storefront → admin)
3. **Part 3 — Test rounds** (prove each layer, then the whole thing)

> You asked for "deployment, then server setup, then tests" — but the server has to be prepared
> **before** you can deploy onto it, so Part 1 is the server setup and Part 2 is the deployment.
> Run them top to bottom.

---

## 0. Read this first — the shape of the system

MyStore is **three apps + one database**, not a single program:

```
                         ┌─────────────────────────────────────────────┐
 Customer ─ https ─────► │ IIS site: MyStore.Storefront  (public cert)  │
                         │   /api/*, /user-content/*  ──► API :8080      │
                         │   everything else          ──► Node SSR :4000 │
                         └─────────────────────────────────────────────┘
                         ┌─────────────────────────────────────────────┐
 Admin ──── https ─────► │ IIS site: MyStore.Admin  (public cert)        │
                         │   /api/*, /user-content/*  ──► API :8080      │
                         │   everything else          ──► index.html     │
                         └─────────────────────────────────────────────┘
                         ┌─────────────────────────────────────────────┐
                         │ IIS site: MyStore.Api  (internal localhost:8080, NO public binding) │
                         │   ASP.NET Core 10 (in-process / ANCM)         │
                         └──────────────────────┬──────────────────────┘
                                                 │ SQL connection (DefaultConnection)
                                                 ▼
                         ┌─────────────────────────────────────────────┐
                         │ SQL Server (separate machine)  DB = MyStore  │
                         └─────────────────────────────────────────────┘
```

| Component | What it is | Tech | How it runs |
|---|---|---|---|
| **Store.Api** | Backend REST API | ASP.NET Core **.NET 10**, EF Core, JWT | IIS site, in-process (ANCM), internal `localhost:8080` |
| **Storefront** | Customer shop | **Angular SSR** (server-rendered) | **Node.js** process (`server.mjs`, port 4000) as a Windows service, IIS reverse-proxies to it |
| **Admin** | Admin panel | **Angular SPA** (static) | Plain static files in IIS, SPA fallback |
| **Database** | Data | **SQL Server** | Separate machine you already have |

### ⚠️ Two things that constrain this deployment

**A. The frontend is prebuilt and cannot be rebuilt from this package.**
The `web/` folder contains `node_modules`, `angular.json`, and a finished `dist/` — but **no source
code and no `package.json`**. So you cannot run `npm run build` here; you deploy `web/dist` as-is.
The prebuilt bundle is compiled with `production:true`, `apiBaseUrl:""` (browser uses same-origin
`/api` — correct for the reverse-proxy topology) and `ssrApiBaseUrl:""`.

⚠️ **The empty `ssrApiBaseUrl` HARD-BREAKS the storefront (502), it does not degrade gracefully.**
On the server side the renderer resolves API calls against its own origin (`http://localhost:4000`),
fetches **itself**, gets HTML back, fails to `JSON.parse` it, throws, and the whole render dies — IIS
then returns **502**. (Earlier wording here claimed "pages still load after hydration" — that is
WRONG; SSR throws first.) Because there's no source to rebuild and **no env var** for it, you must
**patch the deployed server bundle** to set `ssrApiBaseUrl:"http://localhost:8080"` — see §2.3, step
"Patch ssrApiBaseUrl". Leave `apiBaseUrl:""` alone (the browser needs same-origin `/api`). The proper
fix, if you ever get the full source, is to rebuild with `ssrApiBaseUrl: 'http://localhost:8080'`.

**A2. The prebuilt bundle has TWO more baked-in gotchas that must be patched (see §2.3):** an empty
Angular SSR **host allowlist** (rejects every request) and the host-header port-stripping quirk. All
the storefront bundle patches are listed in the **Redeploy checklist** at the end of Part 2.

**B. A fresh production database self-seeds (admin + locations + catalog) — but not the schema.**
On every startup, in **all environments**, the backend now runs three idempotent seeders in order:
`IdentitySeeder` (admin role/user, customer role, guest account) → `LocationSeeder` (country JO, 12
governorates, Main Warehouse) → `CatalogSeeder` (1,391 products + 10 categories from
`catalog.seed.json`). So a fresh Production DB comes up fully populated and products are orderable —
**no `.bak` restore required**. The one thing NOT done automatically is applying the **schema**
(EF migrations) — do that as a deploy step (§2.1) before first start. Product **images** are a
separate matter — see caveat C.

**C. Catalog images reference remote PSD URLs, not local files.**
`catalog.seed.json` stores image paths as `https://e-shop.psd.gov.jo/media/...`. After seeding,
product images therefore hotlink to the live PSD site (or 404 if it's unreachable). The 1,398 files
already in `user-content/` do **not** match those URLs. Localising media (downloading + rewriting to
local `/user-content`) is a separate task — see `Store.Migrator/20_localize_media.ps1`. It does not
block the data seeding.

### Fill these in before you start (your real values)

Values in **bold** are already set throughout this runbook (this deployment). Items left as
`<PLACEHOLDER>` are host-specific/secret — fill them in.

| Name | Meaning | This deployment |
|---|---|---|
| **`crc.onlinepay.ae`** | Public hostname for the shop | set (storefront) |
| **`admin.onlinepay.ae`** | Public hostname for admin | set (admin) |
| `<SQL_HOST>` | SQL Server instance (remote) | fill in — e.g. `10.0.0.20,6545` |
| **`OnlinePayDb`** | Database name | set |
| **`atbnonprodadmin`** / `<DB_PWD>` | SQL login the API uses | user set; password is yours |
| `<JWT_KEY>` | Random ≥32-char secret for signing tokens | generate, see §2.2 |
| **`admin@mystore.local`** / **`Admin@123`** | Bootstrap admin login (from the seeder defaults) | set; change `Admin@123` for real prod |

> ⚠️ Both public hostnames must have real **DNS A records → the server's public IP**
> (`20.204.122.112` in this deployment) for outside users. The `hosts`-file trick only resolves
> on the server itself, for local verification.

### 🔁 Reusing this runbook for a NEW deployment (different site/server/customer)

This runbook has **this deployment's real values inlined**. For a new target, do a find-and-replace
of the values below FIRST, then follow the steps verbatim. Everything else (commands, structure,
patches) is reusable as-is.

| Replace this value | With the new… | Appears in |
|---|---|---|
| `crc.onlinepay.ae` | storefront hostname | §2.3 site/binding, `allowedHosts` patch, §2.5/§3 tests |
| `admin.onlinepay.ae` | admin hostname | §2.4 site/binding, §2.5/§3 tests |
| `*.onlinepay.ae` | the new cert's covered domain | §2.3/§2.4 `allowedHosts`; if not a wildcard, list each host |
| `A78590566CAEFCE88A59FCDDD0B466AF18C0F80B` | the new cert thumbprint (from §1.8 import) | §2.3/§2.4 `AddSslCertificate` |
| `OnlinePayDb` | database name | §2.1, §2.2 conn string, §2.6, §3 |
| `atbnonprodadmin` | SQL login | §2.1, §2.2, §2.6, §3 |
| `20.204.122.112` | server public IP | DNS A records / `hosts` |
| `admin@mystore.local` / `Admin@123` | bootstrap admin creds (set in `appsettings.Production.json`) | §2.2, §3 |
| `C:\Repos\simpleEcom\extracted\MyStore` | source path on YOUR build box | §2.1, §2.2 |
| `<SQL_HOST>`, `<DB_PWD>`, `<JWT_KEY>` | still per-deployment secrets — fill in each time | §2.1/§2.2 |

Things that **stay the same** across deployments (don't change): `localhost:8080` (internal API),
`localhost:4000` (internal SSR), `http://localhost:8080` in the `ssrApiBaseUrl` patch,
`X-Forwarded-Proto: https`, the `C:\inetpub\MyStore\*` folder layout, and all five §2.7 patches.

> If the new site is **not** behind a wildcard cert, set `allowedHosts` to the explicit hostnames
> (e.g. `['localhost', 'shop.newdomain.com', 'admin.newdomain.com']`) instead of a `*.` entry.

Folder layout used throughout (adjust if you like):

```
C:\inetpub\MyStore\api          ← Store.Api publish output
C:\inetpub\MyStore\storefront   ← web\dist\storefront  (browser + server)
C:\inetpub\MyStore\admin        ← web\dist\admin\browser
```

---

# PART 1 — SERVER SETUP (one-time)

Do all of this on the **target server**, in an **elevated PowerShell** (Run as administrator),
unless a step says otherwise. Each step has a verify line — don't move on until it passes.

### 1.1 Enable IIS

**Windows Server:**
```powershell
Install-WindowsFeature -Name `
  Web-Server, Web-WebServer, Web-Common-Http, Web-Static-Content, Web-Default-Doc, `
  Web-Http-Errors, Web-Http-Redirect, Web-Http-Logging, Web-Stat-Compression, `
  Web-Dyn-Compression, Web-Filtering, Web-Mgmt-Console `
  -IncludeManagementTools
```

**Windows 10/11 (if the box is a desktop, not Server):**
```powershell
Enable-WindowsOptionalFeature -Online -All -FeatureName `
  IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-StaticContent, `
  IIS-DefaultDocument, IIS-HttpErrors, IIS-HttpRedirect, IIS-ApplicationDevelopment, `
  IIS-WebServerManagementTools, IIS-ManagementConsole, IIS-HttpCompressionStatic
```

**Verify:** `Get-Service W3SVC` → Status `Running`.

### 1.2 Install the .NET 10 ASP.NET Core **Hosting Bundle** (for Store.Api)

Install IIS **first** (done above), then the bundle — it registers the ASP.NET Core Module into IIS.

```powershell
winget install Microsoft.DotNet.HostingBundle.10 --accept-source-agreements --accept-package-agreements
```
If `winget` isn't available, download "**.NET 10.0 — ASP.NET Core Runtime — Hosting Bundle**" from
<https://dotnet.microsoft.com/download/dotnet/10.0> and run the installer.

Then refresh IIS so it loads the new module:
```powershell
net stop was /y
net start w3svc
```

**Verify (in a NEW elevated shell so PATH picks up the freshly-installed `dotnet`):**
```powershell
dotnet --list-runtimes        # expect Microsoft.AspNetCore.App 10.x AND Microsoft.NETCore.App 10.x
Import-Module IISAdministration
Get-WebGlobalModule | Where-Object Name -like "*AspNetCore*"   # expect AspNetCoreModuleV2
```
> If `dotnet` is "not recognized", you're in the shell that was open *before* the install — open a new
> one (the installer updated the machine PATH, but existing shells keep the old PATH snapshot). Same
> gotcha applies to Node (§1.4) and NSSM (§1.5).

### 1.3 Install URL Rewrite + ARR, and enable the proxy (for the front-site → API/Node proxying)

The two front-end sites forward `/api` to the backend using IIS reverse-proxy rules. That needs two
add-on modules:

```powershell
winget install Microsoft.IIS.URLRewrite
winget install Microsoft.IIS.ApplicationRequestRouting
```
(Or download the MSIs: "URL Rewrite 2.1" and "Application Request Routing 3.0".)

Then turn the proxy on at the server level:
```powershell
& "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.webServer/proxy /enabled:"True" /commit:apphost
```

**Verify:** in IIS Manager → click the **server node** → you should see an **"Application Request
Routing Cache"** icon. Open it → *Server Proxy Settings* → **Enable proxy** is ticked.

### 1.4 Install Node.js (for the storefront SSR server)

Install the **LTS (v20+)**, **for all users** (machine-wide, so a service account can run it).
```powershell
winget install OpenJS.NodeJS.LTS
```
**Verify (in a NEW shell so PATH refreshes):** `node -v` → prints `v20.x` or newer.

> **Build machine vs. SSR runtime.** The *server* that only **runs** the prebuilt SSR bundle
> (`server.mjs`) is fine on Node 20+. But the **machine that builds the frontend** (`npm run build`)
> needs **Node ≥ 22.22.3** — the Angular 22 CLI hard-rejects older 22.x. Build on a machine with a
> compatible Node, then copy the `dist/` bundles to the server.

### 1.5 Install NSSM (to run the SSR Node server as a Windows service)

```powershell
winget install NSSM.NSSM
# or: choco install nssm
```
**Verify:** `nssm version`.

### 1.6 Confirm the server can reach the remote SQL Server

The DB is on another machine, so prove network + auth from **this** server before deploying.

```powershell
# TCP reachability to SQL (default port 1433; change if your instance differs)
Test-NetConnection -ComputerName <SQL_HOST_without_instance> -Port 1433
```
If that fails: open SQL Server's firewall, enable TCP/IP in *SQL Server Configuration Manager*, and
(for a named instance) ensure the **SQL Browser** service is running.

You'll test the actual login in Part 2 once the connection string exists.

### 1.7 Create folders + give the future app-pool identities access

```powershell
New-Item -ItemType Directory -Force -Path C:\inetpub\MyStore\api,C:\inetpub\MyStore\storefront,C:\inetpub\MyStore\admin | Out-Null
```
(Permission grants come after the app pools exist, in Part 2.)

### 1.8 TLS certificates

Get the certificates for `crc.onlinepay.ae` and `admin.onlinepay.ae` into the server's **Local Machine →
Personal** store (import a PFX, or use your existing IIS-managed cert). You'll bind them to the two
public sites in Part 2. The API site stays HTTP-internal and needs no cert.

```powershell
# Example: import a PFX into LocalMachine\My
$pwd = Read-Host -AsSecureString "PFX password"
Import-PfxCertificate -FilePath C:\certs\store.pfx -CertStoreLocation Cert:\LocalMachine\My -Password $pwd
```

**Part 1 done.** The server now has: IIS, .NET 10 hosting, URL Rewrite+ARR (proxy on), Node, NSSM,
SQL reachability, folders, and certs.

---

# PART 2 — DEPLOY MYSTORE

Order: **database → backend API → storefront → admin → final wiring.** Build the publishable
artifacts on a **build machine that has the .NET 10 SDK**; copy them to the server.

## 2.1 Database

The schema can come from EF Core migrations or the bundled SQL script. Pick **one**.

**Option A — EF Core migrations (recommended; needs .NET 10 SDK + the API source on the build box):**
```powershell
# the EF tool MUST match EF Core 10 — if you already have an older one, UPDATE it (install alone
# will say "already installed" and leave you on the old major version, which errors against a v10 project):
dotnet tool update --global dotnet-ef     # or: dotnet tool install --global dotnet-ef (first time)
cd C:\Repos\simpleEcom\extracted\MyStore
# point at the target DB just for this command. Two auth styles:
#  - SQL auth (remote DB):      User ID=atbnonprodadmin;Password=<DB_PWD>
#  - Windows auth (local DB):   Integrated Security=True   (drop User ID/Password)
$env:ConnectionStrings__DefaultConnection = "Data Source=<SQL_HOST>;Initial Catalog=OnlinePayDb;User ID=atbnonprodadmin;Password=<DB_PWD>;Encrypt=True;TrustServerCertificate=True"
dotnet ef database update --project Store.Data --startup-project Store.Api
```
This creates `OnlinePayDb` with all 11 migrations applied (schema only).

> **Build fails with "file locked by Store.Api (PID)"?** A previous run of the API is still holding
> its `.exe`. Kill it first: `Get-Process Store.Api -ErrorAction SilentlyContinue | Stop-Process -Force`.
> **Run from the build box, not the deploy folder** — `dotnet ef`/`dotnet publish` need the source
> (`.csproj`), which lives in the source tree, NOT in `C:\inetpub\MyStore\api` (that holds compiled
> output only). Also confirm the build box can reach SQL: `Test-NetConnection <SQL_HOST> -Port 1433`.

> **Using an EXISTING SQL login/database?** If `atbnonprodadmin` (or your DB) already exists, do NOT
> run the `CREATE LOGIN` block below — it errors or resets the password. If that login is already a
> sysadmin, skip the whole `CREATE LOGIN/USER/ROLE` block; just point the connection string at it.

**Option B — bundled schema script (run on the SQL Server via SSMS or sqlcmd):**
`supported-doc\my_store_shema.sql` creates the `MyStore` database and full schema. Edit the
`FILENAME =` data/log paths near the top to match your SQL Server, then:
```powershell
sqlcmd -S <SQL_HOST> -E -i "C:\...\supported-doc\my_store_shema.sql"
```

**Create the application SQL login** (run on the SQL Server):
```sql
USE master;
IF SUSER_ID(N'atbnonprodadmin') IS NULL
    CREATE LOGIN [atbnonprodadmin] WITH PASSWORD = N'<DB_PWD>', CHECK_POLICY = ON;
GO
USE [OnlinePayDb];
IF USER_ID(N'atbnonprodadmin') IS NULL CREATE USER [atbnonprodadmin] FOR LOGIN [atbnonprodadmin];
ELSE ALTER USER [atbnonprodadmin] WITH LOGIN = [atbnonprodadmin];   -- fixes orphaned user after a restore
ALTER ROLE db_datareader ADD MEMBER [atbnonprodadmin];
ALTER ROLE db_datawriter ADD MEMBER [atbnonprodadmin];
GO
```

**Getting catalog DATA in — now automatic.**
Once the schema exists (above), data seeding happens on API startup in **all environments** (see §0
caveat B): `IdentitySeeder` → `LocationSeeder` → `CatalogSeeder` run in order, all idempotent. So as
soon as the API points at the migrated DB and starts, you get the admin/guest users, Jordan
locations + Main Warehouse, and all 1,391 products + 10 categories — **no `.bak` and no manual
import needed**. Just make sure `catalog.seed.json` ships next to `Store.Api.dll` in the publish
output (it does by default).

Remaining manual bits:
- **Schema** — apply migrations once (Option A or B above) before first start.
- **Images** — see §0 caveat C; catalog images point at remote PSD URLs, localising them is separate.

## 2.2 Backend — Store.Api

**Publish (on the build machine with the .NET 10 SDK):**
```powershell
cd C:\Repos\simpleEcom\extracted\MyStore
dotnet restore
dotnet publish .\Store.Api\Store.Api.csproj -c Release -o .\publish\api
```
Copy the contents of `.\publish\api` to `C:\inetpub\MyStore\api` on the server.

**Production config — create `C:\inetpub\MyStore\api\appsettings.Production.json`.**
`appsettings.json` ships **without** a `Jwt:Key` and **without** an `AdminUser:Password` on purpose —
you must supply both here, or the API won't sign tokens / create the admin:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=<SQL_HOST>;Initial Catalog=OnlinePayDb;User ID=atbnonprodadmin;Password=<DB_PWD>;Encrypt=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Key": "<JWT_KEY>",
    "Issuer": "MyStore",
    "Audience": "MyStoreClients",
    "ExpiryMinutes": 60
  },
  "AdminUser": {
    "Email": "admin@mystore.local",
    "FullName": "Store Administrator",
    "Password": "Admin@123"
  }
}
```
Generate a strong JWT key:
```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```
> ⚠️ **Filename gotcha:** create this with a real editor, but Notepad silently appends `.txt`
> (`appsettings.Production.json.txt`) and Explorer hides it — then the API never loads it and dies with
> `Connection string 'DefaultConnection' was not found.` on startup. Verify the true name:
> `Get-ChildItem C:\inetpub\MyStore\api\appsettings*.json* -Force | Select Name,Length`. The file must
> be **valid JSON** (a stray comma, or a `\`/`"` in the password, invalidates the whole file → same
> "not found" error). Validate: `(Get-Content $f -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection`.

**Create the IIS site (internal-only, port 8080, No Managed Code):**
```powershell
Import-Module WebAdministration
New-WebAppPool -Name "MyStore.Api"
Set-ItemProperty IIS:\AppPools\MyStore.Api -Name managedRuntimeVersion -Value ""   # "No Managed Code"
New-Website -Name "MyStore.Api" -PhysicalPath "C:\inetpub\MyStore\api" `
            -ApplicationPool "MyStore.Api" -Port 8080 -HostHeader "localhost"
```

**Force Production + enable forwarded headers.** Edit the `web.config` that publish produced in
`C:\inetpub\MyStore\api\web.config`. Two env vars are REQUIRED:
- `ASPNETCORE_ENVIRONMENT=Production` — so it loads `appsettings.Production.json` (Jwt:Key, conn
  string) and keeps HTTPS-redirection on (the API only skips redirect in Development).
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` — **without this, login/checkout return 500.** TLS
  terminates at IIS and the API is reached over plain HTTP on :8080, but the antiforgery cookie is
  `SecurePolicy=Always` and throws `the current request is not an SSL request`. This env var turns on
  the ForwardedHeaders middleware (no code change) so the API trusts `X-Forwarded-Proto: https` that
  the front sites send (§2.3/§2.4 set that header). The publish-generated `<aspNetCore .../>` is
  self-closing — convert it to open/close with an `<environmentVariables>` child:
```xml
<aspNetCore processPath="dotnet" arguments=".\Store.Api.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ASPNETCORE_FORWARDEDHEADERS_ENABLED" value="true" />
  </environmentVariables>
</aspNetCore>
```
The front sites are allowed to send that header only after you whitelist the server variable **once**
(server-level):
```powershell
& "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.webServer/rewrite/allowedServerVariables /+"[name='HTTP_X_FORWARDED_PROTO']" /commit:apphost
```

**Grant the app pool write access to its data + media folders.** Create the folders FIRST — they
don't exist until the app's first run, and `icacls` on a missing path fails with
`The system cannot find the file specified`:
```powershell
New-Item -ItemType Directory -Force -Path C:\inetpub\MyStore\api\App_Data,C:\inetpub\MyStore\api\user-content,C:\inetpub\MyStore\api\logs | Out-Null
icacls "C:\inetpub\MyStore\api\App_Data"      /grant "IIS AppPool\MyStore.Api:(OI)(CI)M"
icacls "C:\inetpub\MyStore\api\user-content"  /grant "IIS AppPool\MyStore.Api:(OI)(CI)M"
icacls "C:\inetpub\MyStore\api\logs"          /grant "IIS AppPool\MyStore.Api:(OI)(CI)M"
```
Each should report `Successfully processed 1 files`. `user-content/` holds uploaded product images.
**Exclude it from future redeploys** so uploads survive (see the redeploy checklist).

**Verify (from the server):**
```powershell
curl.exe -i http://localhost:8080/api/catalog/products   # expect 200 + JSON (array may be empty if no data yet)
curl.exe -i http://localhost:8080/swagger/index.html     # expect 404 in Production = correct
```
**Startup failure (`502.5`/`500.30`)?** Note the API only starts on the first request (IIS
in-process) — a `curl` *is* the trigger. To see the real exception: set `stdoutLogEnabled="true"`,
`Restart-WebAppPool -Name "MyStore.Api"`, hit the URL, then read `…\api\logs\stdout_*.log`. Or read
the Event Log directly (already captured, no restart needed):
```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='IIS AspNetCore Module V2'} -MaxEvents 3 | Format-List TimeCreated, Message
```
Common exceptions seen here:
- **`Connection string 'DefaultConnection' was not found`** → `appsettings.Production.json` not loaded
  → wrong filename (`.txt`), invalid JSON, or `ASPNETCORE_ENVIRONMENT` not `Production`. See the
  filename gotcha above.
- **`the current request is not an SSL request`** (on login/POST) → the forwarded-headers wiring is
  missing — see the `ASPNETCORE_FORWARDEDHEADERS_ENABLED` + `X-Forwarded-Proto` steps above and §2.3/§2.4.
- **`SqlException` / cannot open database** → migration not applied, wrong `<SQL_HOST>`/port, or login
  failed. Verify: `sqlcmd -S <SQL_HOST> -U atbnonprodadmin -P <DB_PWD> -d OnlinePayDb -Q "SELECT 1"`.

When done debugging, set `stdoutLogEnabled="false"` again (the EF query logging is very verbose).

## 2.3 Storefront (Angular SSR) — deploy the prebuilt bundle

> No rebuild is possible from this package (no source). Deploy `web\dist\storefront` as-is, then apply
> the TWO required bundle patches below (host allowlist + `ssrApiBaseUrl`) — without them the
> storefront returns "host not allowed" and then 502. See §0 caveat A.

**Copy the WHOLE `storefront\` folder** (browser, server, AND `prerendered-routes.json` at its root —
don't cherry-pick just `browser\`+`server\`):
```powershell
Copy-Item "C:\Repos\simpleEcom\extracted\MyStore\web\dist\storefront\*" "C:\inetpub\MyStore\storefront\" -Recurse -Force
```
> Note: the SSR build emits `browser\index.csr.html` (no plain `index.html`) — that's normal; the Node
> SSR server renders `/`, so the storefront site needs no `index.html`.

**PATCH 1 — `ssrApiBaseUrl` (REQUIRED, or every page 502s).** The bundle bakes `ssrApiBaseUrl:""`, so
SSR fetches its own origin, gets HTML, fails to parse JSON, and dies → 502. There's no env var for it;
patch the SERVER bundle to point at the API. Do NOT touch `apiBaseUrl` (browser must stay same-origin):
```powershell
$files = @(
  "C:\inetpub\MyStore\storefront\server\main.server.mjs",
  "C:\inetpub\MyStore\storefront\server\chunk-A4NI6REV.mjs"   # confirm name: grep server\ for ssrApiBaseUrl
)
foreach ($f in $files) {
  $c = (Get-Content $f -Raw) -replace 'ssrApiBaseUrl:""', 'ssrApiBaseUrl:"http://localhost:8080"'
  [System.IO.File]::WriteAllText($f, $c)   # UTF-8 without BOM
}
# verify: ssrApiBaseUrl now points at :8080, apiBaseUrl still ""
Select-String -Path $files -Pattern 'ssrApiBaseUrl:"[^"]*"|apiBaseUrl:"[^"]*"'
```

**Run the SSR Node server as a Windows service (port 4000):**
```powershell
nssm install MyStoreSSR "C:\Program Files\nodejs\node.exe" "C:\inetpub\MyStore\storefront\server\server.mjs"
nssm set MyStoreSSR AppDirectory "C:\inetpub\MyStore\storefront"
nssm set MyStoreSSR AppEnvironmentExtra PORT=4000
nssm set MyStoreSSR Start SERVICE_AUTO_START
nssm start MyStoreSSR
```

**PATCH 2 — Angular SSR host allowlist (REQUIRED, or every request is rejected).**
The bundle validates the incoming `Host` header against an allowlist. It ships **empty**
(`allowedHosts: []`), so without this fix every request — including the proxied `crc.onlinepay.ae`
traffic — dies with `Header "host" with value "..." is not allowed.` Two gotchas that waste hours:
- The `NG_ALLOWED_HOSTS` **env var does not work** in this prebuilt bundle — you must edit the
  manifest file directly.
- The validator **strips the port** before matching (it parses the header through `new URL()` and
  compares `hostname`), so the value must be `localhost`, **not** `localhost:4000`, even though the
  error text prints the port.

Edit `C:\inetpub\MyStore\storefront\server\angular-app-engine-manifest.mjs` so `allowedHosts` lists
the hostnames (no ports). PowerShell one-liner:
```powershell
$m = "C:\inetpub\MyStore\storefront\server\angular-app-engine-manifest.mjs"
(Get-Content $m -Raw) -replace "allowedHosts:\s*\[[^\]]*\]", "allowedHosts: ['localhost', '*.onlinepay.ae']" | Set-Content $m -Encoding UTF8
nssm restart MyStoreSSR
```
`localhost` covers the local test AND the IIS-proxied request (IIS forwards `Host: localhost:4000`,
which parses to hostname `localhost`); `*.onlinepay.ae` covers the real public host.

**Verify:**
```powershell
curl.exe http://localhost:4000/    # server-rendered HTML (no "host not allowed" error)
```

**Create the public IIS site + reverse-proxy `web.config`:**
```powershell
New-WebAppPool -Name "MyStore.Storefront"
Set-ItemProperty IIS:\AppPools\MyStore.Storefront -Name managedRuntimeVersion -Value ""
New-Website -Name "MyStore.Storefront" -PhysicalPath "C:\inetpub\MyStore\storefront\browser" `
            -ApplicationPool "MyStore.Storefront" -Port 443 -HostHeader "crc.onlinepay.ae" -Ssl -SslFlags 1
# attach the *.onlinepay.ae wildcard cert (thumbprint from §1.8) to this SNI binding:
(Get-WebBinding -Name "MyStore.Storefront" -Protocol https -HostHeader "crc.onlinepay.ae").AddSslCertificate("A78590566CAEFCE88A59FCDDD0B466AF18C0F80B","My")
```
Create `C:\inetpub\MyStore\storefront\browser\web.config`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- API + uploaded media → backend (keep same-origin so cookies/XSRF work).
             serverVariables tells the API the original request was HTTPS (TLS ends at IIS) — REQUIRED
             or login/checkout 500 with "current request is not an SSL request". Needs the one-time
             allowedServerVariables whitelist from §2.2. -->
        <rule name="ProxyApi" stopProcessing="true">
          <match url="^(api|user-content)/(.*)" />
          <serverVariables>
            <set name="HTTP_X_FORWARDED_PROTO" value="https" />
          </serverVariables>
          <action type="Rewrite" url="http://localhost:8080/{R:1}/{R:2}" />
        </rule>
        <!-- Everything else → Node SSR server -->
        <rule name="ProxySsr" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://localhost:4000/{R:1}" />
        </rule>
      </rules>
    </rewrite>
    <proxy preserveHostHeader="true" />
  </system.webServer>
</configuration>
```

## 2.4 Admin (Angular SPA) — deploy the prebuilt bundle

**Copy keeping the `browser\` folder** → the admin's `index.html` lives in `…\admin\browser`, and the
IIS site root + web.config must point THERE (not `…\admin`), or you get a 404 / wrong root:
```powershell
Copy-Item "C:\Repos\simpleEcom\extracted\MyStore\web\dist\admin\browser" "C:\inetpub\MyStore\admin\" -Recurse -Force
Test-Path C:\inetpub\MyStore\admin\browser\index.html   # must be True — this is the site root
```

**Create the public IIS site (PhysicalPath = the `browser` folder):**
```powershell
New-WebAppPool -Name "MyStore.Admin"
Set-ItemProperty IIS:\AppPools\MyStore.Admin -Name managedRuntimeVersion -Value ""
New-Website -Name "MyStore.Admin" -PhysicalPath "C:\inetpub\MyStore\admin\browser" `
            -ApplicationPool "MyStore.Admin" -Port 443 -HostHeader "admin.onlinepay.ae" -Ssl -SslFlags 1
# attach the same *.onlinepay.ae wildcard cert (thumbprint from §1.8) to this SNI binding:
(Get-WebBinding -Name "MyStore.Admin" -Protocol https -HostHeader "admin.onlinepay.ae").AddSslCertificate("A78590566CAEFCE88A59FCDDD0B466AF18C0F80B","My")
```
Create `C:\inetpub\MyStore\admin\browser\web.config` — the file must contain **only** the XML below.
(If you build it in PowerShell with a here-string, run it at the `PS>` prompt; don't paste the
`$var = @'…'@` wrapper into the file itself, or IIS returns `500.19 — not well-formed XML`.)
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- API + uploaded media → backend. X-Forwarded-Proto = REQUIRED for admin login (see §2.2). -->
        <rule name="ProxyApi" stopProcessing="true">
          <match url="^(api|user-content)/(.*)" />
          <serverVariables>
            <set name="HTTP_X_FORWARDED_PROTO" value="https" />
          </serverVariables>
          <action type="Rewrite" url="http://localhost:8080/{R:1}/{R:2}" />
        </rule>
        <!-- SPA fallback: serve index.html for any non-file route -->
        <rule name="SpaFallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
    <proxy preserveHostHeader="true" />
  </system.webServer>
</configuration>
```

## 2.5 Final wiring

- **DNS / hosts:** point `crc.onlinepay.ae` (storefront) and `admin.onlinepay.ae` (admin) at the
  server's IP. Both are single-level subdomains of `onlinepay.ae`, so the one wildcard cert covers
  them. (For a quick local demo without DNS, add both names to the server's
  `C:\Windows\System32\drivers\etc\hosts` pointing at the server IP.)
- **Certs:** both public sites share the **one wildcard cert** `*.onlinepay.ae`
  (thumbprint `A78590566CAEFCE88A59FCDDD0B466AF18C0F80B`, imported in §1.8). The binding commands in
  §2.3/§2.4 attach it via SNI — confirm with `Get-WebBinding -Protocol https | Select-Object
  bindingInformation, certificateHash`.
- **API stays internal:** the `MyStore.Api` site has no public binding — only the two front sites
  reach it via `/api`. No CORS config is needed (everything is same-origin through the proxy).

## 2.6 Localize product images (post-deploy, on the server)

Catalog images are seeded as **remote PSD URLs** (`https://e-shop.psd.gov.jo/media/...`). Until you
localize them, product images hotlink to the live PSD site — fine for visitors who can reach it,
fragile otherwise. `Store.Migrator/20_localize_media.ps1` downloads each remote image into the API's
`user-content` folder and repoints the DB rows at the local copy. It is **idempotent** (only rows
still `LIKE 'http%'` are processed; files are named `m{MediaId}.ext`) and re-runs retry failures.

> Run this **on the server** (or any host that can reach `e-shop.psd.gov.jo` AND the SQL Server),
> **after** the API has started at least once so the catalog is seeded. Point `-OutDir` at the
> **deployed** API's `user-content` so files land where the running app serves them.

```powershell
powershell -ExecutionPolicy Bypass -File .\Store.Migrator\20_localize_media.ps1 `
  -Server "<SQL_HOST>" -Database "OnlinePayDb" -User "atbnonprodadmin" -Password "<DB_PWD>" `
  -OutDir "C:\inetpub\MyStore\api\user-content"
```

It prints `Done: N localized, M failed`. Any failures kept their external URL and are retried on the
next run. **Verify:** `sqlcmd -S <SQL_HOST> -U atbnonprodadmin -P <DB_PWD> -d OnlinePayDb -Q "SELECT COUNT(*)
FROM Media WHERE FileName LIKE 'http%'"` should trend to `0`. Then reload a product page — images
now come from `https://crc.onlinepay.ae/user-content/m####.png`.

> **Can't reach PSD from this build machine?** Confirmed: `e-shop.psd.gov.jo:443` is blocked from
> outside the PSD/Jordan network, so images **cannot** be pre-baked into the publish folder — this
> step must run somewhere with network line-of-sight to the PSD host (typically the production
> server itself).

## 2.7 Redeploy checklist — patches that get WIPED when you re-copy the bundles

Several fixes live **inside the deployed files**, so re-copying `web\dist\*` or the API publish output
silently reverts them and the symptoms come back (502, "host not allowed", login 500). After ANY
redeploy, re-apply:

| # | File (deployed) | Patch | Symptom if missing |
|---|---|---|---|
| 1 | ~~`storefront\server\main.server.mjs`~~ | ~~`ssrApiBaseUrl:"http://localhost:8080"`~~ — **now baked into source** (`environment.ts`); no manual patch needed as long as the API stays on internal `:8080` | storefront 502 (SSR self-fetch) |
| 2 | `storefront\server\angular-app-engine-manifest.mjs` | `allowedHosts: ['localhost', '*.onlinepay.ae']` | `Header "host" ... is not allowed` |
| 3 | `storefront\browser\web.config` + `admin\browser\web.config` | `X-Forwarded-Proto: https` server var on `ProxyApi` | login/checkout 500 (antiforgery) |
| 4 | `api\web.config` | `ASPNETCORE_ENVIRONMENT=Production` + `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` | 500.30 / login 500 |
| 5 | `api\appsettings.Production.json` | conn string + `Jwt:Key` + `AdminUser:Password` | won't start |

Also: **exclude `api\user-content\`** from API redeploys (uploaded images), and re-grant `icacls` if you
recreated folders. Server-level settings (ARR proxy `/enabled`, `allowedServerVariables`, the imported
cert) survive redeploys — you don't re-do those.

> Patch **1 is now eliminated** — the frontend **source is in this repo**, and `ssrApiBaseUrl:'http://localhost:8080'`
> is baked into `web/projects/storefront/src/environments/environment.ts`, so a fresh `npm run build` already
> ships it. Only patches 2–5 remain. (Patch 2 still requires editing the generated SSR manifest until the
> real host allowlist is wired into the build config.)

> **Bilingual content (translations).** The API ships `translations.en.json` (English, full catalog) and
> `translations.ar.json` (Arabic overrides for the rows whose source text contained English). On boot,
> `LocalizationSeeder` idempotently upserts these into `LocalizedContentProperty` (cultures `en-US` /
> `arabic`); the storefront serves them via `Accept-Language`. They're part of the API publish output and
> re-seed automatically on redeploy — nothing to re-patch. To refresh translations, regenerate the JSON
> files and redeploy.

> **Dev secrets are no longer published.** `appsettings.Development.json` (dev connection string / JWT key /
> admin password) is excluded from `dotnet publish` (`CopyToPublishDirectory="Never"`), so only the
> server's `appsettings.Production.json` supplies real config.

**Deployment done.** Proceed to the test rounds.

---

# PART 3 — TEST ROUNDS

Run these in order. Each test states the **command/action**, the **expected result**, and what a
**failure** points to. Don't proceed to the next round until the current one is green.

### Round 0 — Server prerequisites (sanity)
| Check | Command | Pass |
|---|---|---|
| IIS up | `Get-Service W3SVC` | Running |
| .NET 10 runtime | `dotnet --list-runtimes` | `AspNetCore.App 10.x` present |
| ANCM module | `Get-WebGlobalModule \| ? Name -like *AspNetCore*` | `AspNetCoreModuleV2` |
| ARR proxy on | IIS → server → ARR → Server Proxy Settings | "Enable proxy" ticked |
| Node | `node -v` | v20+ |
| NSSM | `nssm version` | prints version |

### Round 1 — Database
| Check | How | Pass |
|---|---|---|
| TCP reach | `Test-NetConnection <SQL_HOST_host> -Port 1433` | `TcpTestSucceeded : True` |
| Login works | `sqlcmd -S <SQL_HOST> -U atbnonprodadmin -P <DB_PWD> -d OnlinePayDb -Q "SELECT DB_NAME()"` | prints `OnlinePayDb` |
| Schema present | `... -Q "SELECT COUNT(*) FROM sys.tables"` | non-zero |
| (If data loaded) | `... -Q "SELECT COUNT(*) FROM Product"` | expected product count |

### Round 2 — Backend API in isolation (on the server)
| Check | Command | Pass |
|---|---|---|
| API process boots | browse/curl `http://localhost:8080/api/catalog/products` | 200 + JSON |
| Prod hardening | `curl.exe -i http://localhost:8080/swagger/index.html` | **404** (Swagger off in Prod) |
| DB-backed read | products endpoint returns rows (if data loaded) | array non-empty |
| Admin seed | `sqlcmd ... -Q "SELECT Email FROM AspNetUsers"` (or the users table) | `admin@mystore.local` present |
| Auth works | `POST /api/auth/login` with admin creds (see below) | 200 + sets cookies / returns token |

Login smoke (adjust endpoint/body to the real contract — check `Store.Api/Controllers`):
```powershell
curl.exe -i -X POST http://localhost:8080/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{\"email\":\"admin@mystore.local\",\"password\":\"Admin@123\"}'
```
**Failure → ** `502.5`/`500.30` = hosting bundle/env problem (enable `stdoutLogEnabled`, read
`api\logs`). DB error on read = connection string / login / orphaned user (re-run §2.1 `ALTER USER`).

### Round 3 — Storefront (SSR + proxy)
| Check | Command | Pass |
|---|---|---|
| Node SSR alive | `curl.exe http://localhost:4000/` | server-rendered HTML |
| Service auto-start | `Get-Service MyStoreSSR` | Running, StartType Automatic |
| Site serves | `curl.exe -k https://crc.onlinepay.ae/` | HTML |
| `/api` proxied | `curl.exe -k https://crc.onlinepay.ae/api/catalog/products` | 200 + JSON (same data as :8080) |
| Media proxied | open a product image URL `https://crc.onlinepay.ae/user-content/...` | image loads |

**Note on the SSR caveat:** `curl https://crc.onlinepay.ae/` may return an HTML shell **without** catalog
data baked in (because `ssrApiBaseUrl` is empty in the prebuilt bundle). That's expected here — the
real test is the browser round (Round 5), where data fills in after hydration.

### Round 4 — Admin (SPA + proxy)
| Check | Command | Pass |
|---|---|---|
| Site serves | `curl.exe -k https://admin.onlinepay.ae/` | `index.html` |
| Deep-link refresh | open `https://admin.onlinepay.ae/products/1` then reload | loads (no 404 → SPA fallback works) |
| `/api` proxied | `curl.exe -k https://admin.onlinepay.ae/api/catalog/products` | 200 + JSON |

### Round 5 — End-to-end functional (real browser)
**Storefront (as a customer):**
1. Open `https://crc.onlinepay.ae/` → catalog renders (data appears after load).
2. Open a product → details + images load.
3. Add to cart as a guest → cart updates.
4. Proceed to checkout → prompted to sign in / register; the auth cookie + XSRF flow works
   (no 401 loop after refresh).

**Admin (as the administrator):**
1. Open `https://admin.onlinepay.ae/` → sign in with `admin@mystore.local` / `Admin@123`.
2. CRUD pages load (products, orders, etc.).
3. Upload a product image → it saves and renders from `/user-content/...`.
4. Refresh a deep link → stays logged in, page loads.

**Failure → ** "login works then 401 after refresh" almost always means requests aren't reaching
`/api` **same-origin** — confirm the `ProxyApi` rule is first and `preserveHostHeader="true"`.

### Round 6 — Production hardening checks
| Check | How | Pass |
|---|---|---|
| Swagger disabled | `https://crc.onlinepay.ae/swagger` and `:8080/swagger` | 404 |
| HTTPS only | hit `http://crc.onlinepay.ae/` | redirects to https |
| Secure cookies | DevTools → Application → Cookies | refresh/XSRF cookies `Secure`, refresh is `HttpOnly`, `SameSite` set |
| API not public | from outside, try `http://<server-ip>:8080/` | not reachable (internal binding only) |
| No dev secrets | grep `appsettings.json` | no real `Jwt:Key` / passwords committed (they live in `appsettings.Production.json`) |

### Round 7 — Resilience
| Check | How | Pass |
|---|---|---|
| Survives reboot | restart the server | all three sites + `MyStoreSSR` come back automatically |
| SSR recovers | `Restart-Service MyStoreSSR` | storefront serves again within seconds |
| App pool recycle | `Restart-WebAppPool MyStore.Api` | API serves again |
| Uploads persist | redeploy API (preserving `user-content`) | previously uploaded images still load |

---

## Redeploy checklist (subsequent releases)

1. **API:** `dotnet publish` → `Stop-WebAppPool MyStore.Api` → copy files **but preserve
   `user-content\` and `appsettings.Production.json`** → `Start-WebAppPool MyStore.Api`.
2. **Storefront/Admin:** copy new `dist\storefront` / `dist\admin\browser` → `Restart-Service
   MyStoreSSR`.
3. **Schema change:** apply EF migrations (or restore an updated DB) in a maintenance window.
4. Re-run Rounds 2–5.

## Quick troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| API `502.5` / `500.30` | Hosting bundle missing/wrong version; check `dotnet --list-runtimes`. Enable `stdoutLogEnabled`, read `api\logs`. |
| API up but DB errors | Wrong connection string, login not mapped, or **orphaned DB user** after a restore → re-run §2.1 `ALTER USER … WITH LOGIN`. |
| `/api` 404 from a front site | ARR proxy not enabled (§1.3), URL Rewrite missing, or `ProxyApi` rule not first. |
| Login OK then 401 after refresh | Requests not same-origin → `apiBaseUrl` must be empty (it is, in the prebuilt bundle) and `/api` proxied on the **same** host with `preserveHostHeader="true"`. |
| Storefront blank / 502 | Node SSR service down → `Get-Service MyStoreSSR`, `curl http://localhost:4000/`. |
| Storefront SSR shows no data (curl) | Expected: `ssrApiBaseUrl` empty in prebuilt bundle; data fills in on client hydration. For true SSR, rebuild from full source with `ssrApiBaseUrl: http://localhost:8080`. |
| Admin deep-link 404 on refresh | SPA fallback rule missing in admin `web.config` (§2.4). |
| Uploaded images 404 | `/user-content` not proxied, or folder wiped on redeploy → exclude it from redeploys (§2.2). |
```
