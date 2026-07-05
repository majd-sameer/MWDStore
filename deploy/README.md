# Deploy config files

Ready-to-use config for the three sites. Full procedure: `../DEPLOYMENT-RUNBOOK.md`.

| File here | Goes on server to |
|---|---|
| `appsettings.Production.template.json` | `C:\inetpub\MyStore\api\appsettings.Production.json` (fill in real values) |
| `storefront.browser.web.config` | `C:\inetpub\MyStore\storefront\browser\web.config` |
| `admin.browser.web.config` | `C:\inetpub\MyStore\admin\browser\web.config` |

## After copying the build artifacts, do these in order

### 1. API (`C:\inetpub\MyStore\api`)
- Create `appsettings.Production.json` from the template (conn string + Jwt:Key + admin password).
- **Patch the published `web.config`** — add the env vars (this is overwritten on every redeploy):
  ```powershell
  $w = "C:\inetpub\MyStore\api\web.config"
  $c = Get-Content $w -Raw
  if ($c -notmatch 'ASPNETCORE_ENVIRONMENT') {
    $c = $c -replace '(<aspNetCore\b[^>]*?)\s*/>', @'
$1>
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        <environmentVariable name="ASPNETCORE_FORWARDEDHEADERS_ENABLED" value="true" />
      </environmentVariables>
    </aspNetCore>
'@
    [System.IO.File]::WriteAllText($w, $c)
  }
  ```
- IIS site/app-pool for the API on internal `:8080` (see runbook §2.2/§4.3).

### 2. Storefront (`C:\inetpub\MyStore\storefront`)
- Copy `storefront.browser.web.config` → `...\storefront\browser\web.config`.
- Install + start the SSR Node service:
  ```powershell
  nssm install MyStoreSSR "C:\Program Files\nodejs\node.exe" "C:\inetpub\MyStore\storefront\server\server.mjs"
  nssm set MyStoreSSR AppDirectory "C:\inetpub\MyStore\storefront"
  nssm set MyStoreSSR AppEnvironmentExtra PORT=4000
  nssm start MyStoreSSR
  ```
- IIS reverse-proxy site on :443 for `crc.onlinepay.ae` (PhysicalPath = `...\storefront\browser`).
- **allowedHosts patch** (overwritten on every storefront redeploy):
  ```powershell
  $m = "C:\inetpub\MyStore\storefront\server\angular-app-engine-manifest.mjs"
  (Get-Content $m -Raw) -replace "allowedHosts:\s*\[[^\]]*\]", "allowedHosts: ['localhost', '*.onlinepay.ae']" | Set-Content $m -Encoding UTF8
  nssm restart MyStoreSSR
  ```

### 3. Admin (`C:\inetpub\MyStore\admin`)
- Copy `admin.browser.web.config` → `...\admin\browser\web.config`.
- IIS site on :443 for `admin.onlinepay.ae` (PhysicalPath = `...\admin\browser`).

### One-time server setup (survives all redeploys — do once)
```powershell
# allow the X-Forwarded-Proto server variable used by the storefront/admin web.configs
& "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.webServer/rewrite/allowedServerVariables /+"[name='HTTP_X_FORWARDED_PROTO']" /commit:apphost
```
Plus: ARR proxy enabled, wildcard cert imported, DNS for the two hostnames. See runbook §1.3/§1.8/§2.5.

## On a later redeploy you only re-touch
- `api\web.config` (re-run the patch above).
- `storefront\server\angular-app-engine-manifest.mjs` (re-run the allowedHosts patch).
- Never delete `api\user-content\` or `api\appsettings.Production.json`.
