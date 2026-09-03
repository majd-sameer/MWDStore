# SECURITY IMPLEMENTATION BLUEPRINT: IIS / AZURE APP SERVICE / .NET HARDENING

## 🎯 Target Objectives
1. Implement defensive security headers via custom ASP.NET Core Middleware.
2. Remove server foot-printing signatures at the IIS gateway level.
3. Establish request payload limits to defend against Denial of Service (DoS) attempts on CMS text payloads.

---

## 🛠️ Task 1: Security Headers Middleware

### 📁 File Target Placement
* **Layer:** Presentation Layer (`Web.API`)
* **Path:** `Presentation/Web.API/Middleware/SecurityHeadersMiddleware.cs`

### 💻 Code Implementation
```csharp
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace YourApplicationName.WebApi.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Prevent Clickjacking framing attacks
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // 2. Prevent MIME-type sniffing vulnerabilities
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // 3. Force browser-side protection filters against legacy XSS
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // 4. Control referrer visibility across origin boundaries
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // 5. Content Security Policy (Adjust inline requirements based on SSR build output)
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " + 
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' https://stripe.com;"); 

        // 6. HTTP Strict Transport Security (HSTS) - Enforce SSL for 1 Year
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");

        await _next(context);
    }
}
```

### ⚙️ Pipeline Registration
Add this integration block at the topmost layer of your HTTP request pipeline inside `Presentation/Web.API/Program.cs`:

```csharp
using YourApplicationName.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);
// ... service registrations

var app = builder.Build();

// CRITICAL ROUTING SEQUENCE: Register security configurations before processing routers
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();

// Enforce production domains explicitly (No Wildcards allowed)
app.UseCors("ProductionCorsPolicy"); 

app.UseAuthorization();
// ... mapping endpoints
```

---

## 🌐 Task 2: IIS Production Web Configuration

### 📁 File Target Placement
* **Layer:** Presentation Layer (`Web.API`) root directory
* **Path:** `Presentation/Web.API/web.config`

### 💻 Configuration Markup
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    
    <!-- Execute the .NET Kestrel Core in-process under IIS hosting extensions -->
    <aspNetCore processPath="dotnet" arguments=".\YourApplicationName.WebApi.dll" stdoutLogEnabled="false" stdoutLogFile="\\?\%home%\LogFiles\stdout" hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>

    <security>
      <requestFiltering removeServerHeader="true">
        <!-- Prevent massive multi-part attacks targeting CMS text fields (50 MB Limit) -->
        <requestLimits maxAllowedContentLength="52428800" />
      </requestFiltering>
    </security>

    <httpProtocol>
      <customHeaders>
        <!-- Obfuscate hosting fingerprints to prevent target version profiling -->
        <remove name="X-Powered-By" />
        <remove name="Server" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
```

---

## 🚨 Architectural Constraints for the Agent
* **Invariant 1:** Do not allow the inclusion of wildcard rules (`*`) inside CORS configurations within `Program.cs`.
* **Invariant 2:** All variables containing access tokens, connection configurations, or database credentials must be fetched from environment contexts or managed services (like Azure Key Vault). They must never exist inside the source codebase files.
