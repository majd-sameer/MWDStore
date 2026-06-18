using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Store.Api.Infrastructure;
using Store.Application;
using Store.Application.Auth;
using Store.Data;
using Store.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// CORS for the local Angular SPA dev servers (storefront 4200, admin 4201).
// Credentials are allowed so the httpOnly refresh cookie and the XSRF cookie flow cross-origin;
// AllowCredentials forbids the wildcard origin, so origins are listed explicitly. Exposed headers
// are the response headers browser JS is allowed to read.
const string SpaCorsPolicy = "SpaCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(SpaCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200", "http://localhost:4201")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("X-Correlation-Id"));
});

// Antiforgery wired to Angular's convention: the SPA reads the request token from the JS-readable
// XSRF-TOKEN cookie (see AuthCookies) and echoes it in X-XSRF-TOKEN. The framework's own cookie-token
// stays httpOnly. Decorate state-changing same-origin endpoints with [ValidateAntiForgeryToken] to
// enforce the double-submit; the cookie-authenticated auth endpoints opt out via [IgnoreAntiforgeryToken]
// and rely on the refresh cookie's SameSite=Strict attribute instead.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AuthCookies.XsrfHeader;
    options.Cookie.Name = "__Host-Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Application + data layers.
builder.Services.AddStoreData(builder.Configuration);
// Payment host options (storefront origin for Stripe Checkout return URLs). Registered before
// AddStoreApplication so its bound instance wins over the library's TryAddSingleton default.
builder.Services.AddSingleton(
    builder.Configuration.GetSection(Store.Application.Payments.PaymentsOptions.SectionName)
        .Get<Store.Application.Payments.PaymentsOptions>() ?? new Store.Application.Payments.PaymentsOptions());
builder.Services.AddStoreApplication();

// Local media storage for admin uploads (product images, etc.).
builder.Services.AddSingleton<IMediaStorage, LocalMediaStorage>();

// ----- Identity (JWT-only API; no cookie schemes) -------------------------------------------------
builder.Services
    .AddIdentityCore<User>(options =>
    {
        // Match SimplCommerce's relaxed password policy so migrated accounts keep working.
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 4;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequiredUniqueChars = 0;

        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<Role>()
    .AddEntityFrameworkStores<StoreDbContext>()
    .AddDefaultTokenProviders();

// JWT options + bearer authentication.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton(jwtOptions);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// Bootstrap admin account options (password lives in gitignored config / user-secrets).
var adminSeedOptions = builder.Configuration.GetSection(AdminSeedOptions.SectionName).Get<AdminSeedOptions>()
    ?? new AdminSeedOptions();
builder.Services.AddSingleton(adminSeedOptions);

// OpenAPI document + Swagger UI (with Bearer auth).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token (without the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc, null), new List<string>() }
    });
});

var app = builder.Build();

// Data seeding — all idempotent + additive, so safe to run on every startup in every environment.
// Order matters: identity (admin/guest users) → locations (country/governorates/warehouse) →
// catalog (needs a user to own products and the warehouse to attach stock).
// NOTE: the schema itself is NOT auto-migrated here — apply EF migrations as a deploy step
// (`dotnet ef database update`) before the app starts against a fresh database.
await IdentitySeeder.SeedAsync(app.Services);
await LocationSeeder.SeedAsync(app.Services);
// Real catalog seeding from catalog.seed.json (no-op when the file is absent).
await CatalogSeeder.SeedAsync(app.Services);
// English content overrides from translations.en.json (no-op when the file is absent).
await LocalizationSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In development the Angular SSR server calls the plain-HTTP endpoint
// (ssrApiBaseUrl = http://localhost:5094); redirecting it to the self-signed
// HTTPS endpoint makes Node's fetch fail, so only redirect outside dev.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Serve uploaded media (saved by LocalMediaStorage) at /user-content/{fileName}.
var userContentPath = Path.Combine(app.Environment.ContentRootPath, LocalMediaStorage.FolderName);
Directory.CreateDirectory(userContentPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(userContentPath),
    RequestPath = LocalMediaStorage.RequestPath
});

app.UseCors(SpaCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();

app.Run();
