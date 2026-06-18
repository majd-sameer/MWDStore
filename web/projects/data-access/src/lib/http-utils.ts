/**
 * Framework-pure helpers shared by the data-access services.
 *
 * IMPORTANT: this library does not configure HttpClient, set a base URL, or
 * attach auth tokens. Paths are root-relative (`/api/...`, matching the Phase 5
 * Postman collection's `{{baseUrl}}/api/...`). The host origin, the
 * `provideHttpClient` setup, and the bearer-token interceptor all live in the
 * `core` library.
 */

/** Root path of the Store.Api surface. The origin is prepended by `core`. */
export const API_ROOT = '/api';

export type QueryParamValue = string | number | boolean | null | undefined;

/**
 * Builds a request params record, dropping `null` / `undefined` / empty-string
 * values so optional filters don't leak onto the query string.
 */
export function toQueryParams(
  source: Record<string, QueryParamValue>,
): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {};
  for (const [key, value] of Object.entries(source)) {
    if (value !== null && value !== undefined && value !== '') {
      params[key] = value;
    }
  }
  return params;
}

/**
 * Storefront catalog filter (GET /api/catalog/products and
 * GET /api/catalog/categories/{categoryId}/products). Field names map to the
 * OpenAPI query parameters (`Query`, `Brand`, `Page`, `PageSize`, ...).
 */
export interface CatalogProductQuery {
  query?: string;
  brand?: string;
  category?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
  minPrice?: number;
  maxPrice?: number;
  /** Keep only products rated at least this value (e.g. 4 or 4.5). */
  minRating?: number;
}

/** Maps a {@link CatalogProductQuery} to the OpenAPI query-parameter names. */
export function catalogQueryParams(
  query: CatalogProductQuery,
): Record<string, string | number | boolean> {
  return toQueryParams({
    Query: query.query,
    Brand: query.brand,
    Category: query.category,
    Page: query.page,
    PageSize: query.pageSize,
    Sort: query.sort,
    MinPrice: query.minPrice,
    MaxPrice: query.maxPrice,
    MinRating: query.minRating,
  });
}

/** Admin product list filter (GET /api/admin/products). */
export interface AdminProductQuery {
  query?: string;
  includeDeleted?: boolean;
  /** Narrow to soft-deleted products only (implies includeDeleted). */
  deletedOnly?: boolean;
  isPublished?: boolean;
  brandId?: number;
  categoryId?: number;
  page?: number;
  pageSize?: number;
}

/** Admin order list filter (GET /api/admin/orders). */
export interface AdminOrderQuery {
  status?: number;
  customerId?: number;
  page?: number;
  pageSize?: number;
}
