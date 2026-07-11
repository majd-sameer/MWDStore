# Media Storage & Display

How images and files are uploaded, stored, and rendered across MyStore — from the admin upload dialog to the disk, and back out to the storefront.

## TL;DR

- **Upload** is admin-only: a multipart `POST /api/admin/media` saves one file per call.
- **On disk**, files land under `Store.Api/user-content/{GUID}.{ext}` — renamed to a GUID, original name kept only as a caption.
- **A `Media` DB row** records metadata (`FileName`, `Caption`, `FileSize`, `MediaType`). Entities (Product, Category, News) reference media by foreign key, never by embedding the file.
- **Serving**: static-file middleware maps `/user-content/{fileName}` to that folder — public, no auth.
- **On read**, the API returns ready-made **root-relative URLs** (`/user-content/...`). The frontend binds them straight to `[src]` — there is no client-side URL builder.
- **No** server-side resizing, thumbnailing, or MIME sniffing. "Thumbnail" means *designated primary image*, not a generated smaller file. Display sizing is pure CSS.

---

## End-to-end flow

```
Admin picks file(s)
   │  multipart POST /api/admin/media  (field name: "file")
   ▼
AdminMediaController.Upload
   │  validate extension → IMediaStorage.SaveAsync → disk (GUID name)
   │  insert Media row (FileName=GUID, Caption=original name)
   ▼
returns MediaDto { id, fileName, url:"/user-content/...", caption, mediaType }
   │
Admin form keeps the id(s); on Save sends ids only:
   │  thumbnailImageId / mediaIds / mediumId   (NOT the files again)
   ▼
Product / News / ContentBlock rows reference Media by FK
   ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄ (read side) ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄
Storefront/admin GET catalog/news/…
   │  service projects FileName → IMediaUrlBuilder.GetUrl → "/user-content/..."
   ▼
JSON carries ready URLs (thumbnailImageUrl, imageUrls, url, mediaUrl…)
   │
Template binds [src]="…"  → browser fetches same-origin /user-content/...
   │  dev: proxy.conf.json  ·  prod: reverse proxy  → Store.Api static files
   ▼
Image renders (broken URL → graceful gradient/initials fallback)
```

---

## Backend

### Storage abstraction

There are **two** parallel abstractions that share the same `/user-content/{fileName}` convention, split by concern:

| Abstraction | Layer | Job |
|---|---|---|
| `IMediaStorage` | `Store.Api` (host) | Write side + disk I/O — save, delete, build URL. Used by admin controllers. |
| `IMediaUrlBuilder` | `Store.Application` | URL-only, no disk access. Used by storefront read paths. Swappable for a CDN/S3 builder without touching read code. |

**`IMediaStorage` / `LocalMediaStorage`** — `Store.Api/Infrastructure/MediaStorage.cs`

```csharp
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken ct = default);
    void Delete(string? fileName);
    string? GetUrl(string? fileName);
}
```

`LocalMediaStorage` saves to `{ContentRootPath}/user-content`:

- **Filename** = `Guid.NewGuid()` + original extension. The original name is *not* used on disk.
- **Delete** is a no-op for absolute (external) URLs and guards against path traversal — it resolves the full path and confirms it stays under the root folder before deleting.
- **GetUrl** returns already-absolute `http(s)://` URLs untouched (seeded/external media); otherwise prefixes `/user-content/`.

**`IMediaUrlBuilder` / `LocalMediaUrlBuilder`** — `Store.Application/Common/IMediaUrlBuilder.cs`

Same URL logic, read-only. Its `IsAbsoluteUrl` helper is the single source of truth reused by `LocalMediaStorage` too. Registered as the default via `TryAddSingleton` in `Store.Application/DependencyInjection.cs`, so a host could override it (e.g. with a CDN builder) without changing any read code.

### Media types

Stored as a plain `int` on `Medium.MediaType` (mirrors SimplCommerce's enum) — `Store.Api/Infrastructure/MediaStorage.cs`:

```csharp
public const int Image = 1;   // inferred from image extensions on upload
public const int File  = 5;   // inferred from document extensions on upload
public const int Video = 10;  // defined but not selectable via upload
```

### `Medium` entity — `Store.Domain/Medium.cs`

```csharp
public class Medium
{
    public long Id { get; set; }
    public string? Caption { get; set; }   // original uploaded filename
    public int FileSize { get; set; }
    public string? FileName { get; set; }  // GUID name on disk, OR an absolute URL for seeded media
    public int MediaType { get; set; }
    // nav collections: Categories, ProductMedia, Products, NewsItems
}
```

Entities reference media by **foreign key**, never by embedding:

| Entity | Primary image | Gallery |
|---|---|---|
| `Product` | `ThumbnailImageId` → `ThumbnailImage` | `ProductMedia` (join, ordered) |
| `Category` | `ThumbnailImageId` → `ThumbnailImage` | — |
| `NewsItem` | `ThumbnailImageId` → `ThumbnailImage` | — |

The gallery join — `Store.Domain/ProductMedium.cs`:

```csharp
public class ProductMedium
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long MediaId { get; set; }
    public int DisplayOrder { get; set; }   // gallery ordering
    // nav: Media, Product
}
```

Persistence: `StoreDbContext` exposes `DbSet<Medium> Media` and `DbSet<ProductMedium> ProductMedia`. Tables, FKs (`FK_Product_Media_ThumbnailImageId`, `FK_Category_…`, `FK_NewsItem_…`, `FK_ProductMedia_Media_MediaId`), and per-FK indexes are created in `Store.Data/Migrations/20260610074410_InitialSchema.cs`.

### Upload endpoint — `Store.Api/Controllers/Admin/AdminMediaController.cs`

- Route `POST /api/admin/media`, guarded by `[Authorize(Policy = AuthPolicies.Media)]`.
- `[RequestSizeLimit(10 MB)]` on the action.
- Validation is **extension-based only** (case-insensitive) — no MIME/content sniffing:
  - Images → `MediaType = 1`: `.jpg .jpeg .png .gif .webp .avif .svg`
  - Files → `MediaType = 5`: `.pdf .doc .docx .xls .xlsx .zip .txt`
  - Anything else → `400`.
- Saves via `IMediaStorage.SaveAsync`, inserts a `Medium` row (`FileName` = GUID, `Caption` = original name), returns:

```csharp
public sealed record MediaDto(long Id, string? FileName, string Url, string? Caption, int MediaType);
```

Upload is **decoupled** from entity save: the client uploads first, gets an `id` + ready-to-use `url`, then references the `id` when saving the product/news/block.

**Attaching media to entities** (elsewhere):
- `AdminProductsController` sets `product.ThumbnailImageId` and reconciles the gallery via `ReconcileMediaAsync`, adding/removing `ProductMedium` rows from the request's `MediaIds` list.
- `AdminNewsController` sets `item.ThumbnailImageId`.

### Read path

Storefront services project the stored `FileName` through `IMediaUrlBuilder.GetUrl(...)` into ready URLs:
- `Store.Application/Catalog/CatalogService.cs` → `ThumbnailImageUrl` + gallery `imageUrls`.
- `Store.Application/ShoppingCart/CartService.cs` → `ProductImageUrl`.

### Wiring — `Store.Api/Program.cs`

```csharp
// DI
builder.Services.AddSingleton<IMediaStorage, LocalMediaStorage>();

// Static file serving — /user-content/{fileName} → the user-content folder
var userContentPath = Path.Combine(app.Environment.ContentRootPath, LocalMediaStorage.FolderName);
Directory.CreateDirectory(userContentPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(userContentPath),
    RequestPath  = LocalMediaStorage.RequestPath   // "/user-content"
});
```

A dedicated `PhysicalFileProvider` scoped to `user-content` (there is no `wwwroot`). Note: `UseStaticFiles` runs **before** `UseCors`/`UseAuthentication`/`UseAuthorization`, so **uploaded media is served publicly** without auth.

### Configuration

There is **none** — all media settings are hard-coded constants, not in `appsettings*.json`:

| Setting | Value | Location |
|---|---|---|
| Storage folder | `user-content` | `LocalMediaStorage.FolderName` |
| Request path | `/user-content` | `LocalMediaStorage.RequestPath` |
| Max upload size | 10 MB | `AdminMediaController.MaxFileSize` + `[RequestSizeLimit]` |
| Allowed extensions | two `HashSet`s | `AdminMediaController` |

No global Kestrel `MaxRequestBodySize` / `MultipartBodyLengthLimit` override exists; the only limit is the per-endpoint `[RequestSizeLimit]`.

---

## Frontend (`web/`)

### Key fact: the API returns ready-made URLs

There is **no client-side URL builder, base-URL prefix, or image pipe**. Every read response embeds root-relative URLs (`thumbnailImageUrl`, `imageUrls`, `thumbnailUrl`, `url`, `mediaUrl`), and templates bind them straight to `[src]`. The browser resolves them same-origin.

### Upload service — `web/projects/data-access/src/lib/admin/admin-media.service.ts`

The **only** upload service in the app:

```ts
upload(file: File): Observable<MediaDto> {
  const body = new FormData();
  body.append('file', file, file.name);
  return this.http.post<MediaDto>(`${API_ROOT}/admin/media`, body); // API_ROOT = '/api'
}
```

`MediaDto` (`web/projects/data-access/src/lib/models.ts`): `{ id, fileName, url, caption, mediaType }`.

**Consumers** (all admin-only; no storefront upload, no `ui`-library upload component):

| Component | What it uploads | What it sends on save |
|---|---|---|
| `products/product-form.ts` | thumbnail (1 file) + gallery (many files, uploaded one-by-one) | `thumbnailImageId`, `mediaIds: number[]` |
| `cms/content-blocks.ts` | per-block image | `mediumId` |
| `cms/news-form.ts` | thumbnail | `thumbnailImageId` |

The pattern everywhere: **upload files first → collect ids → save the record with ids** (files are never re-sent).

### How relative URLs resolve

Image `[src]` attributes are plain browser fetches (not `HttpClient`), so the base-URL interceptor (`web/projects/core/src/lib/interceptors/base-url.interceptor.ts`) does **not** touch them — it only rewrites `HttpClient` `/api` calls, and even then only when a base is configured (`apiBaseUrl` is `''` in the browser). So `/user-content/...` resolves against the page origin and is forwarded by the proxy.

### Proxy — `web/projects/{admin,storefront}/proxy.conf.json` (identical)

```json
{
  "/api":          { "target": "https://localhost:7142", "secure": false, "changeOrigin": true },
  "/user-content": { "target": "https://localhost:7142", "secure": false, "changeOrigin": true }
}
```

Both `/api` and `/user-content` proxy to the API dev host — this is what makes relative image `[src]` values load during `ng serve`. In **production**, `apiBaseUrl` is empty and a reverse proxy is expected to forward both prefixes to Store.Api (keeps the httpOnly refresh cookie + Angular XSRF same-origin, no CORS).

### Display — the `lib-tile` component

`web/projects/ui/src/lib/tile/tile.ts` is the primary reusable art component across the storefront (product cards, categories):

```html
@if (src() && !failed()) {
  <img [src]="src()" [attr.alt]="alt()" class="ui-tile__img" loading="lazy" (error)="failed.set(true)" />
} @else {
  <!-- deterministic gradient tile + glyph/initial, seeded from product name/id -->
}
```

- `loading="lazy"`, and `(error)` degrades a broken URL to a brand gradient fallback.
- Fallback tone is derived deterministically from a `seed`, so a product always gets the same color.

The **product-detail gallery** (`web/projects/storefront/src/app/features/catalog/product-detail.ts`) is ad-hoc (not a `ui` component): it concatenates variation images then base images, de-duplicated, with zoom/lightbox. Admin lists use `[src]="p.thumbnailUrl"` with `(error)` fallbacks; the admin `avatar-cell` shows a circular image falling back to initials.

Models carrying image URLs (`web/projects/data-access/src/lib/models.ts`): `ProductListItem.thumbnailImageUrl`, `ProductDetailModel.{thumbnailImageUrl,imageUrls}`, `CartItemModel.productImageUrl`, `AdminProductDetail.{thumbnailUrl,media[]}`, news DTOs `{thumbnailImageUrl}`. **Categories and brands carry no image URL** in the frontend models.

---

## Notable gaps / things to know

- **No MIME/content validation** — extension check only. `.svg` is allowed, which is an XSS vector if served inline to authenticated origins; consider sniffing or disallowing SVG.
- **No delete HTTP endpoint** — `IMediaStorage.Delete` exists but has no controller call site; orphaned files accumulate on disk (DB rows are reconciled via `ProductMedia`, physical files are not GC'd).
- **No server-side resizing/thumbnailing** — a single full-size file per upload; all sizing is CSS. No ImageSharp/SkiaSharp/System.Drawing anywhere.
- **Media is served publicly** — static-file middleware runs before auth; any `/user-content/{guid}.ext` is reachable without login.
- **Nothing is configurable** — folder, request path, size limit, and allowed extensions are all hard-coded constants.

## Key files

| Concern | Path |
|---|---|
| Storage interface + local impl + media-type constants | `Store.Api/Infrastructure/MediaStorage.cs` |
| Read-side URL builder | `Store.Application/Common/IMediaUrlBuilder.cs` |
| DI + static-file wiring | `Store.Api/Program.cs` |
| Upload endpoint / validation | `Store.Api/Controllers/Admin/AdminMediaController.cs` |
| `Medium` entity / gallery join | `Store.Domain/Medium.cs`, `Store.Domain/ProductMedium.cs` |
| Schema / FKs / indexes | `Store.Data/Migrations/20260610074410_InitialSchema.cs` |
| Read-path consumers | `Store.Application/Catalog/CatalogService.cs`, `Store.Application/ShoppingCart/CartService.cs` |
| Frontend upload service | `web/projects/data-access/src/lib/admin/admin-media.service.ts` |
| Display component | `web/projects/ui/src/lib/tile/tile.ts` |
| Dev proxy | `web/projects/{admin,storefront}/proxy.conf.json` |
