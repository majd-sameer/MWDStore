import { Routes } from '@angular/router';
import { authGuard, roleGuard } from 'core';
import { AdminLayout } from './layout/admin-layout';
import { adminHomeGuard, AREA, STAFF_ROLES } from './core/roles';

/**
 * Public routes (`login`, `forbidden`) render outside the admin chrome.
 * Everything under `AdminLayout` requires being signed in (`authGuard`) and
 * holding at least one staff role (`roleGuard(...STAFF_ROLES)`); both run on the
 * parent so children inherit the baseline. Each child then adds its own
 * `roleGuard(...AREA.x)` so a role only reaches its areas — the sets mirror the
 * API's `AuthPolicies`. The index `''` redirects to the role's home section.
 */
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · MadeWithDetermination Admin',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'forbidden',
    title: 'Access denied · MadeWithDetermination Admin',
    loadComponent: () =>
      import('./features/forbidden/forbidden').then((m) => m.Forbidden),
  },
  {
    path: '',
    component: AdminLayout,
    canActivate: [authGuard, roleGuard(...STAFF_ROLES)],
    canActivateChild: [authGuard, roleGuard(...STAFF_ROLES)],
    children: [
      {
        path: '',
        pathMatch: 'full',
        canActivate: [adminHomeGuard],
        children: [],
      },
      {
        path: 'dashboard',
        title: 'Dashboard · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.reports)],
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        // Living reference for the shared data-table components (spec §6):
        // StatusPill / AvatarCell / TableSkeleton / TableFooter / FilterDropdown
        // across empty, skeleton and populated states. No sidebar link — reach
        // it directly at /design/tables.
        path: 'design/tables',
        title: 'Table components · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/design/table-showcase').then(
            (m) => m.TableShowcase,
          ),
      },
      {
        path: 'products',
        title: 'Products · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/products/product-list').then(
            (m) => m.AdminProductList,
          ),
      },
      {
        path: 'products/:id',
        title: 'Edit product · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/products/product-form').then(
            (m) => m.AdminProductForm,
          ),
      },
      {
        path: 'product-options',
        title: 'Product options · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/catalog-settings/product-options').then(
            (m) => m.AdminProductOptions,
          ),
      },
      {
        path: 'product-options/:id',
        title: 'Edit option · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/catalog-settings/product-option-form').then(
            (m) => m.AdminProductOptionForm,
          ),
      },
      {
        path: 'product-attributes',
        title: 'Product attributes · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/catalog-settings/product-attributes').then(
            (m) => m.AdminProductAttributes,
          ),
      },
      {
        path: 'product-attributes/:id',
        title: 'Edit attribute · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/catalog-settings/product-attribute-form').then(
            (m) => m.AdminProductAttributeForm,
          ),
      },
      {
        path: 'product-templates',
        title: 'Product templates · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/catalog-settings/product-templates').then(
            (m) => m.AdminProductTemplates,
          ),
      },
      {
        path: 'product-templates/:id',
        title: 'Edit template · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/catalog-settings/product-template-form').then(
            (m) => m.AdminProductTemplateForm,
          ),
      },
      {
        path: 'vendors',
        title: 'Vendors · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.vendors)],
        loadComponent: () =>
          import('./features/vendors/vendors').then((m) => m.AdminVendors),
      },
      {
        path: 'vendors/:id',
        title: 'Edit vendor · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.vendors)],
        loadComponent: () =>
          import('./features/vendors/vendor-form').then(
            (m) => m.AdminVendorForm,
          ),
      },
      {
        path: 'contacts',
        title: 'Contacts · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.sales)],
        loadComponent: () =>
          import('./features/contacts/contacts').then((m) => m.AdminContacts),
      },
      {
        path: 'logs',
        title: 'System logs · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.settings)],
        loadComponent: () =>
          import('./features/system/logs').then((m) => m.AdminLogs),
      },
      {
        path: 'audit-log',
        title: 'Audit Log · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.settings)],
        loadComponent: () =>
          import('./features/system/audit-log').then((m) => m.AdminAuditLog),
      },
      {
        path: 'categories',
        title: 'Categories · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/categories/categories').then(
            (m) => m.AdminCategories,
          ),
      },
      {
        path: 'categories/:id',
        title: 'Edit category · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/categories/category-form').then(
            (m) => m.AdminCategoryForm,
          ),
      },
      {
        path: 'brands',
        title: 'Brands · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/brands/brands').then((m) => m.AdminBrands),
      },
      {
        path: 'brands/:id',
        title: 'Edit brand · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.catalog)],
        loadComponent: () =>
          import('./features/brands/brand-form').then((m) => m.AdminBrandForm),
      },
      {
        path: 'orders',
        title: 'Orders · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.orders)],
        loadComponent: () =>
          import('./features/orders/order-list').then((m) => m.AdminOrderList),
      },
      {
        path: 'orders/:id',
        title: 'Order · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.orders)],
        loadComponent: () =>
          import('./features/orders/order-detail').then(
            (m) => m.AdminOrderDetail,
          ),
      },
      {
        path: 'users',
        title: 'Users · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.users)],
        loadComponent: () =>
          import('./features/users/users').then((m) => m.AdminUsers),
      },
      {
        path: 'users/:id',
        title: 'Edit user · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.users)],
        loadComponent: () =>
          import('./features/users/user-form').then((m) => m.AdminUserForm),
      },
      {
        path: 'moderation',
        title: 'Moderation · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.moderation)],
        loadComponent: () =>
          import('./features/moderation/moderation').then(
            (m) => m.AdminModeration,
          ),
      },
      {
        path: 'customers',
        title: 'Customers · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.sales)],
        loadComponent: () =>
          import('./features/customers/customers').then(
            (m) => m.AdminCustomers,
          ),
      },
      {
        path: 'customers/:id',
        title: 'Edit customer · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.sales)],
        loadComponent: () =>
          import('./features/customers/customer-form').then(
            (m) => m.AdminCustomerForm,
          ),
      },
      {
        path: 'inventory',
        title: 'Inventory · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.inventory)],
        loadComponent: () =>
          import('./features/inventory/inventory').then(
            (m) => m.AdminInventory,
          ),
      },
      {
        path: 'stock-out',
        title: 'Stock out · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.inventory)],
        loadComponent: () =>
          import('./features/inventory/stock-out').then((m) => m.AdminStockOut),
      },
      {
        path: 'stock-out-log',
        title: 'Stock-out log · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.inventory)],
        loadComponent: () =>
          import('./features/inventory/stock-out-log').then(
            (m) => m.AdminStockOutLog,
          ),
      },
      {
        path: 'settings',
        title: 'Settings · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.settings)],
        loadComponent: () =>
          import('./features/system/settings').then((m) => m.AdminSettings),
      },
      {
        path: 'locations',
        title: 'Countries & states · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.settings)],
        loadComponent: () =>
          import('./features/system/locations').then((m) => m.AdminLocations),
      },
      {
        path: 'locations/:id',
        title: 'Edit country · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.settings)],
        loadComponent: () =>
          import('./features/system/country-form').then(
            (m) => m.AdminCountryForm,
          ),
      },
      {
        path: 'localization',
        title: 'Localization · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.settings)],
        loadComponent: () =>
          import('./features/system/localization').then(
            (m) => m.AdminLocalization,
          ),
      },
      {
        path: 'payments',
        title: 'Payments · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.payments)],
        loadComponent: () =>
          import('./features/payments/payments').then((m) => m.AdminPayments),
      },
      {
        path: 'payments/Stripe',
        title: 'Configure Stripe · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.payments)],
        loadComponent: () =>
          import('./features/payments/payment-stripe-form').then(
            (m) => m.AdminPaymentStripeForm,
          ),
      },
      {
        path: 'payments/PaypalExpress',
        title: 'Configure PayPal Express · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.payments)],
        loadComponent: () =>
          import('./features/payments/payment-paypal-express-form').then(
            (m) => m.AdminPaymentPaypalExpressForm,
          ),
      },
      {
        path: 'payments/MEPS',
        title: 'Configure MEPS · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.payments)],
        loadComponent: () =>
          import('./features/payments/payment-meps-form').then(
            (m) => m.AdminPaymentMepsForm,
          ),
      },
      {
        path: 'payments/:id',
        title: 'Configure provider · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.payments)],
        loadComponent: () =>
          import('./features/payments/payment-provider-form').then(
            (m) => m.AdminPaymentProviderForm,
          ),
      },
      {
        path: 'pages',
        title: 'Pages · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/pages').then((m) => m.AdminPages),
      },
      {
        path: 'pages/:id',
        title: 'Edit page · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/page-form').then((m) => m.AdminPageForm),
      },
      {
        path: 'menus',
        title: 'Menus · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/menus').then((m) => m.AdminMenus),
      },
      {
        path: 'menus/:id',
        title: 'Edit menu · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/menu-form').then((m) => m.AdminMenuForm),
      },
      {
        path: 'news',
        title: 'News · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/news').then((m) => m.AdminNews),
      },
      {
        path: 'site-content',
        pathMatch: 'full',
        redirectTo: 'site-content/home',
      },
      {
        path: 'site-content/:page',
        title: 'Site content · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/content-blocks').then((m) => m.AdminContentBlocks),
      },
      {
        path: 'news/:id',
        title: 'Edit article · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.content)],
        loadComponent: () =>
          import('./features/cms/news-form').then((m) => m.AdminNewsForm),
      },
      {
        path: 'promotions',
        title: 'Promotions · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.marketing)],
        loadComponent: () =>
          import('./features/promotions/promotions').then(
            (m) => m.AdminPromotions,
          ),
      },
      {
        path: 'promotions/:id',
        title: 'Edit promotion · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.marketing)],
        loadComponent: () =>
          import('./features/promotions/promotion-form').then(
            (m) => m.AdminPromotionForm,
          ),
      },
      {
        path: 'warehouses',
        title: 'Warehouses · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.inventory)],
        loadComponent: () =>
          import('./features/warehouses/warehouses').then(
            (m) => m.AdminWarehouses,
          ),
      },
      {
        path: 'warehouses/:id',
        title: 'Edit warehouse · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.inventory)],
        loadComponent: () =>
          import('./features/warehouses/warehouse-form').then(
            (m) => m.AdminWarehouseForm,
          ),
      },
      {
        path: 'taxes',
        title: 'Taxes · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.taxes)],
        loadComponent: () =>
          import('./features/tax/taxes').then((m) => m.AdminTaxes),
      },
      {
        path: 'taxes/:id',
        title: 'Edit tax rate · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.taxes)],
        loadComponent: () =>
          import('./features/tax/tax-rate-form').then((m) => m.AdminTaxRateForm),
      },
      {
        path: 'shipping',
        title: 'Shipping · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.fulfillment)],
        loadComponent: () =>
          import('./features/shipping/shipping').then((m) => m.AdminShipping),
      },
      {
        path: 'shipping/:id',
        title: 'Edit table rate · MadeWithDetermination Admin',
        canActivate: [roleGuard(...AREA.fulfillment)],
        loadComponent: () =>
          import('./features/shipping/table-rate-form').then(
            (m) => m.AdminTableRateForm,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
