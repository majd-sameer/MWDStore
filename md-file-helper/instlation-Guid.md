# MyStore — IIS Installation & Deployment Guide

Comprehensive, step-by-step instructions to publish the **Backend API** (`Store.Api`), the
**Storefront** (Angular SSR) and the **Admin** (Angular SPA) on a Windows / IIS server, plus how
to **restore the database** (schema **and** data) from a backup.

> Conventions used in this guide
> - `<SERVER>` — the IIS host name (e.g. `store.example.com`).
> - Example site layout (adjust to your hostnames):
>   | App | Public URL | IIS site | Physical path |
>   |-----|-----------|----------|---------------|
>   | Storefront (SSR) | `https://store.example.com` | `MyStore.Storefront` | `C:\inetpub\MyStore\storefront` |
>   | Admin (SPA) | `https://admin.example.com` | `MyStore.Admin` | `C:\inetpub\MyStore\admin` |
>   | Backend API | internal `http://localhost:8080` | `MyStore.Api` | `C:\inetpub\MyStore\api` |
> - Both front-end apps are designed to run **same-origin** behind a reverse proxy that forwards
>   `/api` and `/user-content` to `Store.Api`. This keeps the httpOnly refresh-token cookie and
>   Angular's XSRF protection working **without CORS**. Do **not** expose the API on its own public
>   origin for the SPA — keep it behind each site's `/api` path.

---

## 1. Architecture at a glance

```
Browser ──► https://store.example.com  (IIS: MyStore.Storefront)
                 │  /api/*, /user-content/*  ──(URL Rewrite/ARR)──►  http://localhost:8080  (Store.Api)
                 └  everything else           ──(reverse proxy)────►  Node SSR server (server.mjs)

Browser ──► https://admin.example.com  (IIS: MyStore.Admin, static SPA)
                 │  /api/*, /user-content/*  ──(URL Rewrite/ARR)──►  http://localhost:8080  (Store.Api)
                 └  everything else           ──(SPA fallback)──────►  index.html
```

- **Backend** — `Store.Api`, ASP.NET Core on **.NET 10**, SQL Server via EF Core
  (connection string name `DefaultConnection`). Hosted in-process by the ASP.NET Core Module (ANCM).
- **Storefront** — Angular 22 **SSR** app. Build produces a Node/Express server
  (`dist/storefront/server/server.mjs`) + static browser assets (`dist/storefront/browser`).
  Needs **Node.js** on the server.
- **Admin** — Angular 22 **CSR/SPA**. Build produces static files only (`dist/admin/browser`).
  Pure static hosting + SPA fallback rewrite.

---

## 2. Server prerequisites (install once)

Run an elevated **PowerShell** for these steps.

### 2.1 Enable IIS + required features
```powershell
Enable-WindowsOptionalFeature -Online -FeatureName `
  IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-StaticContent, `
  IIS-DefaultDocument, IIS-HttpErrors, IIS-HttpRedirect, IIS-ApplicationDevelopment, `
  IIS-NetFxExtensibility45, IIS-ISAPIExtensions, IIS-ISAPIFilter, `
  IIS-WebServerManagementTools, IIS-ManagementConsole, IIS-HttpCompressionStatic `
  -All
```

### 2.2 .NET 10 Hosting Bundle (for Store.Api)
Download and install the **.NET 10 ASP.NET Core Hosting Bundle** (installs the .NET runtime +
ANCM v2 IIS module). After installing, restart the web server:
```powershell
net stop was /y
net start w3svc
```
Verify:
```powershell
dotnet --list-runtimes      # expect Microsoft.AspNetCore.App 10.x and Microsoft.NETCore.App 10.x
```

### 2.3 URL Rewrite + Application Request Routing (ARR) — for the reverse proxy
Install both (from Microsoft / Web Platform Installer or standalone MSIs):
- **URL Rewrite 2.1**
- **Application Request Routing 3.0**

Then enable the proxy at server level:
- IIS Manager → server node → **Application Request Routing Cache** → **Server Proxy Settings** →
  check **Enable proxy** → Apply.

(Equivalent via `appcmd`:)
```powershell
%windir%\system32\inetsrv\appcmd.exe set config -section:system.webServer/proxy /enabled:"True" /commit:apphost
```

### 2.4 Node.js (for the Storefront SSR server)
Install **Node.js LTS (20.x or newer)** for *all users* (machine-wide):
```powershell
node -v   # confirm it resolves for the service account too
```

> The server only **runs** the prebuilt SSR bundle, so Node 20+ is fine here. The machine that
> **builds** the frontend (`npm run build`, §5.2) needs **Node ≥ 22.22.3** — the Angular 22 CLI
> rejects older 22.x.

### 2.5 SQL Server
You need a reachable SQL Server instance (local or remote). Install **SQL Server** + **SQL Server
Management Studio (SSMS)** (or `sqlcmd`). Note the instance name — the dev default in this repo is
`MSALEH\SQL` with database `MyStore`; change to your server's instance.

---

## 3. Restore the database (schema **and** data)

A SQL Server backup (`.bak`) contains both the schema and the data, so a single restore brings the
database fully online. **Do this before deploying the API.**

### 3.1 Copy the backup to the SQL Server host
Place the `.bak` somewhere the SQL Server **service account** can read, e.g.
`C:\SQLBackups\MyStore.bak`.

### 3.2 Inspect the logical file names inside the backup
```sql
RESTORE FILELISTONLY FROM DISK = N'C:\SQLBackups\MyStore.bak';
```
Note the two `LogicalName` values (typically `MyStore` for the data file and `MyStore_log` for the
log).

### 3.3 Restore (creates the database with schema + data)
Run in SSMS (or `sqlcmd`). Adjust logical names and target paths to match your server:
```sql
USE master;
GO
-- If a stale copy exists, drop incoming connections first:
IF DB_ID(N'MyStore') IS NOT NULL
BEGIN
    ALTER DATABASE [MyStore] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END
GO

RESTORE DATABASE [MyStore]
FROM DISK = N'C:\SQLBackups\MyStore.bak'
WITH
    MOVE N'MyStore'     TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQL\MSSQL\DATA\MyStore.mdf',
    MOVE N'MyStore_log' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQL\MSSQL\DATA\MyStore_log.ldf',
    REPLACE,           -- overwrite an existing DB of the same name
    RECOVERY,          -- bring DB online when done
    STATS = 5;
GO

ALTER DATABASE [MyStore] SET MULTI_USER;
GO
```

Command-line equivalent:
```powershell
sqlcmd -S "<SERVER>\SQL" -E -i "C:\SQLBackups\restore-mystore.sql"
```

### 3.4 Create / map the application login
Create a SQL login the API will use and map it to the restored database with the right role.
Restoring a DB can leave **orphaned users** (the DB user's SID no longer matches a server login), so
re-map them:
```sql
USE master;
GO
IF SUSER_ID(N'mystore_app') IS NULL
    CREATE LOGIN [mystore_app] WITH PASSWORD = N'<StrongPassword!>', CHECK_POLICY = ON;
GO
USE [MyStore];
GO
-- If a DB user already exists from the backup, relink it; otherwise create it:
IF USER_ID(N'mystore_app') IS NULL
    CREATE USER [mystore_app] FOR LOGIN [mystore_app];
ELSE
    ALTER USER [mystore_app] WITH LOGIN = [mystore_app];
ALTER ROLE db_datareader ADD MEMBER [mystore_app];
ALTER ROLE db_datawriter ADD MEMBER [mystore_app];
-- The API runs schema-stable; db_owner is only needed if you apply EF migrations against this DB.
GO
```

### 3.5 (Optional) Verify the restore
```sql
USE [MyStore];
SELECT COUNT(*) AS Products  FROM Product;
SELECT COUNT(*) AS Users     FROM [User];
SELECT TOP 5 name FROM sys.tables ORDER BY name;
```

> **Schema-only / fresh database instead of a backup?**
> The schema is also reproducible from EF Core migrations (in `Store.Data/Migrations`). From a
> machine with the .NET 10 SDK and `dotnet-ef` installed:
> ```powershell
> dotnet tool install --global dotnet-ef
> dotnet ef database update --project Store.Data --startup-project Store.Api
> ```
> Identity (admin role + bootstrap admin user) is seeded automatically on API startup. The product
> catalog is seeded from `Store.Api/catalog.seed.json` **only in the Development environment** — in
> Production, data must come from the restored backup (or be imported manually).

---

## 4. Build & publish the **Backend** (`Store.Api`)

Do these on a build machine that has the **.NET 10 SDK**.

### 4.1 Publish
```powershell
dotnet restore
dotnet publish .\Store.Api\Store.Api.csproj -c Release -o .\publish\api
```
Copy the contents of `.\publish\api` to the server, e.g. `C:\inetpub\MyStore\api`.

> The publish output includes `catalog.seed.json` + `translations.en.json` + `translations.ar.json`
> (bilingual content, seeded into `LocalizedContentProperty` on boot — idempotent). `appsettings.Development.json`
> is **excluded** from publish, so no dev secrets ship; supply real config via `appsettings.Production.json` (§4.2).

### 4.2 Production configuration
The API reads `ConnectionStrings:DefaultConnection` and a JWT signing key. **Never ship dev
secrets.** Create `C:\inetpub\MyStore\api\appsettings.Production.json` (this file overrides
`appsettings.json` when `ASPNETCORE_ENVIRONMENT=Production`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=<SERVER>\\SQL;Initial Catalog=MyStore;User ID=mystore_app;Password=<StrongPassword!>;Encrypt=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Key": "<a-long-random-production-signing-key-at-least-32-chars>",
    "Issuer": "MyStore",
    "Audience": "MyStoreClients",
    "ExpiryMinutes": 60
  },
  "AdminUser": {
    "Email": "admin@yourdomain.com",
    "FullName": "Store Administrator",
    "Password": "<StrongAdminPassword!>"
  }
}
```
Notes:
- Generate a strong `Jwt:Key` (e.g. `[Convert]::ToBase64String((1..48|%{Get-Random -Max 256}))`).
- `AdminUser` is used to **bootstrap** the admin login on first run (idempotent). Change it from the
  repo defaults (`admin@mystore.local` / `Admin@123`).
- Prefer `Encrypt=True` with a real certificate; `TrustServerCertificate=True` is acceptable for a
  trusted internal SQL host.

### 4.3 Create the IIS site for the API
- App pool: **No Managed Code**, identity = a dedicated account (e.g. `ApplicationPoolIdentity`)
  that can reach SQL Server.
- Bind it to an **internal-only** port, e.g. `http://localhost:8080` (no public binding — it is
  reached only via each front-end site's `/api` reverse proxy).

```powershell
Import-Module WebAdministration
New-WebAppPool -Name "MyStore.Api"
Set-ItemProperty IIS:\AppPools\MyStore.Api -Name managedRuntimeVersion -Value ""   # No Managed Code
New-Website -Name "MyStore.Api" -PhysicalPath "C:\inetpub\MyStore\api" `
            -ApplicationPool "MyStore.Api" -Port 8080 -HostHeader "localhost"
```

### 4.4 Set the environment & verify
The publish output already contains a `web.config` with the ASP.NET Core Module handler. Ensure the
environment is **Production** (set in `web.config` `<environmentVariables>` or per-site):
```xml
<aspNetCore processPath="dotnet" arguments=".\Store.Api.dll" stdoutLogEnabled="false" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  </environmentVariables>
</aspNetCore>
```
Outside Development the API enables **HTTPS redirection**; behind the reverse proxy it receives
forwarded plain HTTP, so terminate TLS at the front-end sites (Section 7) and keep the API binding
internal.

Quick check from the server:
```powershell
curl http://localhost:8080/swagger/index.html   # Swagger is Development-only; expect 404 in Production = OK
curl http://localhost:8080/api/catalog/products  # adjust to a real GET endpoint; expect JSON / 200
```

### 4.5 Uploaded media folder (`user-content`)
The API serves uploaded media from `user-content/` under its content root at `/user-content/...`.
The folder is auto-created on startup. Ensure the app-pool identity has **read/write** here and that
this folder is **excluded from redeploys** (or moved to a persistent path) so uploads survive
deployments:
```powershell
icacls "C:\inetpub\MyStore\api\user-content" /grant "IIS AppPool\MyStore.Api:(OI)(CI)M"
```

---

## 5. Build & publish the **Storefront** (Angular SSR)

The storefront renders on the server with Node, so the SSR server must know an **absolute** API URL
it can reach server-side (browser calls stay relative via the reverse proxy).

### 5.1 Configure the production environment
> **Already pre-set in this repo.** `environment.ts` ships with `ssrApiBaseUrl: 'http://localhost:8080'`
> and `apiBaseUrl: ''`, matching the values below — only change it if the API's internal binding (§4.3)
> isn't `:8080`. A fresh `npm run build` bakes it into the SSR bundle (no post-deploy patch needed).

Confirm `web/projects/storefront/src/environments/environment.ts`:
```ts
export const environment = {
  production: true,
  apiBaseUrl: '',                              // keep empty → browser stays same-origin (/api)
  ssrApiBaseUrl: 'http://localhost:8080',      // absolute API URL reachable from the SSR Node process
};
```
- `apiBaseUrl: ''` → browser requests go to `/api` on the storefront origin and are reverse-proxied
  to the API (cookies + XSRF stay same-origin).
- `ssrApiBaseUrl` → the API origin the **Node SSR server** can reach directly (the internal API
  binding from §4.3).

### 5.2 Build
From `web/`:
```powershell
cd web
npm ci --legacy-peer-deps    # ng-bootstrap@20 needs --legacy-peer-deps on Angular 22
npm run build                # builds libs (data-access, util, ui, core) then storefront + admin
```
This produces:
- `web/dist/storefront/server/server.mjs` + `web/dist/storefront/browser/` (SSR app)
- `web/dist/admin/browser/` (admin SPA — see §6)

Copy `web/dist/storefront/` to the server, e.g. `C:\inetpub\MyStore\storefront`.

### 5.3 Run the SSR Node server as a Windows service
The SSR server listens on `process.env.PORT` (default **4000**). Run it as a resilient background
service. **Option A — NSSM (recommended, simple):**
```powershell
# Install nssm (choco install nssm) then:
nssm install MyStoreSSR "C:\Program Files\nodejs\node.exe" "C:\inetpub\MyStore\storefront\server\server.mjs"
nssm set MyStoreSSR AppDirectory "C:\inetpub\MyStore\storefront"
nssm set MyStoreSSR AppEnvironmentExtra PORT=4000
nssm start MyStoreSSR
```
**Option B — PM2** (`npm i -g pm2 pm2-windows-startup`) running `server.mjs` with `PORT=4000`.

Verify the Node app is up:
```powershell
curl http://localhost:4000/        # expect server-rendered HTML
```

### 5.4 Create the Storefront IIS site (reverse proxy front)
This IIS site terminates TLS, forwards `/api` + `/user-content` to the API, and forwards everything
else to the Node SSR server.

- Create site `MyStore.Storefront` → path `C:\inetpub\MyStore\storefront\browser`, public binding
  (`https://store.example.com`, your certificate).
- Add a `web.config` at `C:\inetpub\MyStore\storefront\browser\web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- API and uploaded media → backend (keep same-origin for cookies/XSRF) -->
        <rule name="ProxyApi" stopProcessing="true">
          <match url="^(api|user-content)/(.*)" />
          <action type="Rewrite" url="http://localhost:8080/{R:1}/{R:2}" />
        </rule>
        <!-- Everything else → Node SSR server -->
        <rule name="ProxySsr" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://localhost:4000/{R:1}" />
        </rule>
      </rules>
    </rewrite>
    <!-- Pass the original host/scheme so the API builds correct URLs and cookies -->
    <proxy preserveHostHeader="true" />
  </system.webServer>
</configuration>
```
> ARR proxy must be **enabled** at server level (§2.3) for these `Rewrite`-to-URL rules to act as a
> reverse proxy. Static browser assets (`/browser`) are also served by the SSR server, so routing
> everything to Node is fine; the API/media rule must come **first**.

---

## 6. Build & publish the **Admin** (Angular SPA)

The admin app is pure static + a SPA fallback. It was already built in §5.2.

- Copy `web/dist/admin/browser/` to the server, e.g. `C:\inetpub\MyStore\admin`.
- Create IIS site `MyStore.Admin` → path `C:\inetpub\MyStore\admin`, public binding
  (`https://admin.example.com`, certificate).
- Add `C:\inetpub\MyStore\admin\web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <!-- API and uploaded media → backend (same-origin) -->
        <rule name="ProxyApi" stopProcessing="true">
          <match url="^(api|user-content)/(.*)" />
          <action type="Rewrite" url="http://localhost:8080/{R:1}/{R:2}" />
        </rule>
        <!-- SPA fallback: serve index.html for any non-file, non-api route -->
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
    <staticContent>
      <!-- Ensure correct MIME types for modern assets if needed -->
      <remove fileExtension=".json" /><mimeMap fileExtension=".json" mimeType="application/json" />
    </staticContent>
  </system.webServer>
</configuration>
```

The admin production environment (`web/projects/admin/src/environments/environment.ts`) already uses
`apiBaseUrl: ''` (same-origin) — no change needed.

---

## 7. TLS, hosts & final wiring

1. **Certificates** — bind a valid TLS cert to the two public sites (`store.*`, `admin.*`). The API
   site stays HTTP on `localhost:8080` (internal only).
2. **DNS / hosts** — point `store.example.com` and `admin.example.com` at the server.
3. **Forwarded headers** — the API trusts the reverse proxy for HTTPS redirection; keeping the API
   internal and TLS-terminated at the front sites avoids redirect loops. If you later expose the API
   directly, configure `UseForwardedHeaders`.
4. **CORS** — not used in this topology (everything is same-origin via the proxies). The dev CORS
   policy only whitelists `http://localhost:4200/4201`.

---

## 8. Post-deployment verification

```powershell
# Backend (internal)
curl http://localhost:8080/api/<a-public-GET-endpoint>          # 200 + JSON

# Storefront
curl https://store.example.com/                                 # SSR HTML
curl https://store.example.com/api/<same-endpoint>              # 200 (proxied to API)

# Admin
curl https://admin.example.com/                                 # index.html
curl https://admin.example.com/api/<same-endpoint>              # 200 (proxied to API)
```
Then in a browser:
- Storefront: browse catalog, add to cart as guest, proceed to sign-in at checkout.
- Admin: sign in with the `AdminUser` credentials from §4.2, confirm CRUD pages load and images
  (`/user-content/...`) render.

---

## 9. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `HTTP 502.5 / 500.30` on API site | .NET 10 Hosting Bundle missing or wrong version; check `dotnet --list-runtimes`. Enable `stdoutLogEnabled` in `web.config` to read startup errors. |
| API starts but DB errors | Connection string wrong, SQL login not mapped, or **orphaned DB user** after restore → re-run §3.4 `ALTER USER ... WITH LOGIN`. |
| `/api` returns 404 from a front site | ARR proxy not enabled (§2.3), or the `ProxyApi` rewrite rule isn't first / URL Rewrite not installed. |
| Login works then 401 after refresh | Cookies/XSRF broke because requests aren't same-origin → ensure `apiBaseUrl` is empty and `/api` is proxied on the **same** host (`preserveHostHeader="true"`). |
| Storefront pages blank / 502 | Node SSR service down → check `MyStoreSSR` service and `curl http://localhost:4000/`. Confirm Node is installed machine-wide. |
| SSR renders but data missing | `ssrApiBaseUrl` not reachable from the Node process → set it to the internal API URL (`http://localhost:8080`) and rebuild. |
| Admin deep-link (`/products/123`) 404 on refresh | SPA fallback rule missing in admin `web.config` (§6). |
| Uploaded images 404 | `/user-content` not proxied, or the API's `user-content` folder was wiped on redeploy → exclude/move it (§4.5). |
| `npm ci` peer-dep errors | Use `npm ci --legacy-peer-deps` (ng-bootstrap@20 / Angular 22). |

---

## 10. Redeploy checklist (subsequent releases)

1. `dotnet publish` the API → stop `MyStore.Api` app pool → copy files (**preserve `user-content/`
   and `appsettings.Production.json`**) → start app pool.
2. `npm run build` in `web/` → copy `dist/storefront` and `dist/admin/browser` → `nssm restart
   MyStoreSSR`.
3. If the schema changed, apply EF migrations or restore an updated backup (§3) during a maintenance
   window.
4. Smoke-test with §8.
