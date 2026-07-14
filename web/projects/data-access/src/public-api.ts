/*
 * Public API Surface of data-access
 *
 * Framework-pure TypeScript models + typed Angular services generated from
 * Store.Api's OpenAPI document and the Phase 5 Postman collection.
 *
 * Reads are exposed as `httpResource` factories; commands return `Observable`s
 * from `HttpClient`. This library performs NO HTTP wiring: no base-URL config,
 * no `provideHttpClient`, no auth-token handling — those live in `core`.
 */

// Models & shared query helpers
export * from './lib/models';
export {
  API_ROOT,
  toQueryParams,
  type QueryParamValue,
  type QueryParamScalar,
  type PagedResult,
  type CatalogProductQuery,
  type AdminProductQuery,
  type AdminOrderQuery,
} from './lib/http-utils';

// Shared reactive state
export * from './lib/locale-state';

// Storefront services
export * from './lib/auth.service';
export * from './lib/account.service';
export * from './lib/catalog.service';
export * from './lib/cart.service';
export * from './lib/checkout.service';
export * from './lib/locations.service';
export * from './lib/order.service';
export * from './lib/payments.service';
export * from './lib/storefront-features.service';
export * from './lib/content-blocks.service';

// Admin services
export * from './lib/admin/admin-dashboard.service';
export * from './lib/admin/admin-brands.service';
export * from './lib/admin/admin-categories.service';
export * from './lib/admin/admin-products.service';
export * from './lib/admin/admin-orders.service';
export * from './lib/admin/admin-inventory.service';
export * from './lib/admin/admin-media.service';
export * from './lib/admin/admin-product-options.service';
export * from './lib/admin/admin-product-attributes.service';
export * from './lib/admin/admin-tax.service';
export * from './lib/admin/admin-shipping.service';
export * from './lib/admin/admin-warehouses.service';
export * from './lib/admin/admin-locations.service';
export * from './lib/admin/admin-promotions.service';
export * from './lib/admin/admin-users.service';
export * from './lib/admin/admin-customers.service';
export * from './lib/admin/admin-moderation.service';
export * from './lib/admin/admin-cms.service';
export * from './lib/admin/admin-payments.service';
export * from './lib/admin/admin-system.service';
export * from './lib/admin/admin-operations.service';
export * from './lib/admin/admin-audit.service';
export * from './lib/admin/admin-content-blocks.service';
export * from './lib/admin/admin-dev-assistant.service';
