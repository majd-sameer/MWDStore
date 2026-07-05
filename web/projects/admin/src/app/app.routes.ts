import { Routes } from '@angular/router';
import { authGuard, roleGuard } from 'core';
import { AdminLayout } from './layout/admin-layout';

/**
 * Public routes (`login`, `forbidden`) render outside the admin chrome.
 * Everything under `AdminLayout` is gated by `authGuard` (must be signed in)
 * then `roleGuard('Admin')` (must hold the Admin role); both guards run on the
 * parent so child routes inherit them.
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
    // Store.Api seeds the role as the lowercase string "admin" (AppRoles.Admin),
    // which is exactly what the JWT role claim carries — so the guard matches
    // that value, not a title-cased "Admin".
    canActivate: [authGuard, roleGuard('admin')],
    canActivateChild: [authGuard, roleGuard('admin')],
    children: [
      {
        path: '',
        title: 'Dashboard · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'products',
        title: 'Products · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/products/product-list').then(
            (m) => m.AdminProductList,
          ),
      },
      {
        path: 'products/:id',
        title: 'Edit product · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/products/product-form').then(
            (m) => m.AdminProductForm,
          ),
      },
      {
        path: 'product-options',
        title: 'Product options · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/catalog-settings/product-options').then(
            (m) => m.AdminProductOptions,
          ),
      },
      {
        path: 'product-options/:id',
        title: 'Edit option · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/catalog-settings/product-option-form').then(
            (m) => m.AdminProductOptionForm,
          ),
      },
      {
        path: 'product-attributes',
        title: 'Product attributes · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/catalog-settings/product-attributes').then(
            (m) => m.AdminProductAttributes,
          ),
      },
      {
        path: 'product-attributes/:id',
        title: 'Edit attribute · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/catalog-settings/product-attribute-form').then(
            (m) => m.AdminProductAttributeForm,
          ),
      },
      {
        path: 'product-templates',
        title: 'Product templates · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/catalog-settings/product-templates').then(
            (m) => m.AdminProductTemplates,
          ),
      },
      {
        path: 'product-templates/:id',
        title: 'Edit template · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/catalog-settings/product-template-form').then(
            (m) => m.AdminProductTemplateForm,
          ),
      },
      {
        path: 'vendors',
        title: 'Vendors · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/vendors/vendors').then((m) => m.AdminVendors),
      },
      {
        path: 'vendors/:id',
        title: 'Edit vendor · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/vendors/vendor-form').then(
            (m) => m.AdminVendorForm,
          ),
      },
      {
        path: 'contacts',
        title: 'Contacts · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/contacts/contacts').then((m) => m.AdminContacts),
      },
      {
        path: 'logs',
        title: 'System logs · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/system/logs').then((m) => m.AdminLogs),
      },
      {
        path: 'categories',
        title: 'Categories · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/categories/categories').then(
            (m) => m.AdminCategories,
          ),
      },
      {
        path: 'categories/:id',
        title: 'Edit category · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/categories/category-form').then(
            (m) => m.AdminCategoryForm,
          ),
      },
      {
        path: 'brands',
        title: 'Brands · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/brands/brands').then((m) => m.AdminBrands),
      },
      {
        path: 'brands/:id',
        title: 'Edit brand · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/brands/brand-form').then((m) => m.AdminBrandForm),
      },
      {
        path: 'orders',
        title: 'Orders · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/orders/order-list').then((m) => m.AdminOrderList),
      },
      {
        path: 'orders/:id',
        title: 'Order · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/orders/order-detail').then(
            (m) => m.AdminOrderDetail,
          ),
      },
      {
        path: 'users',
        title: 'Users · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/users/users').then((m) => m.AdminUsers),
      },
      {
        path: 'users/:id',
        title: 'Edit user · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/users/user-form').then((m) => m.AdminUserForm),
      },
      {
        path: 'moderation',
        title: 'Moderation · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/moderation/moderation').then(
            (m) => m.AdminModeration,
          ),
      },
      {
        path: 'customers',
        title: 'Customers · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/customers/customers').then(
            (m) => m.AdminCustomers,
          ),
      },
      {
        path: 'customers/:id',
        title: 'Edit customer · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/customers/customer-form').then(
            (m) => m.AdminCustomerForm,
          ),
      },
      {
        path: 'inventory',
        title: 'Inventory · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/inventory/inventory').then(
            (m) => m.AdminInventory,
          ),
      },
      {
        path: 'settings',
        title: 'Settings · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/system/settings').then((m) => m.AdminSettings),
      },
      {
        path: 'locations',
        title: 'Countries & states · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/system/locations').then((m) => m.AdminLocations),
      },
      {
        path: 'locations/:id',
        title: 'Edit country · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/system/country-form').then(
            (m) => m.AdminCountryForm,
          ),
      },
      {
        path: 'localization',
        title: 'Localization · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/system/localization').then(
            (m) => m.AdminLocalization,
          ),
      },
      {
        path: 'payments',
        title: 'Payments · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/payments/payments').then((m) => m.AdminPayments),
      },
      {
        path: 'payments/Stripe',
        title: 'Configure Stripe · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/payments/payment-stripe-form').then(
            (m) => m.AdminPaymentStripeForm,
          ),
      },
      {
        path: 'payments/PaypalExpress',
        title: 'Configure PayPal Express · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/payments/payment-paypal-express-form').then(
            (m) => m.AdminPaymentPaypalExpressForm,
          ),
      },
      {
        path: 'payments/MEPS',
        title: 'Configure MEPS · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/payments/payment-meps-form').then(
            (m) => m.AdminPaymentMepsForm,
          ),
      },
      {
        path: 'payments/:id',
        title: 'Configure provider · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/payments/payment-provider-form').then(
            (m) => m.AdminPaymentProviderForm,
          ),
      },
      {
        path: 'pages',
        title: 'Pages · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/cms/pages').then((m) => m.AdminPages),
      },
      {
        path: 'pages/:id',
        title: 'Edit page · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/cms/page-form').then((m) => m.AdminPageForm),
      },
      {
        path: 'menus',
        title: 'Menus · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/cms/menus').then((m) => m.AdminMenus),
      },
      {
        path: 'menus/:id',
        title: 'Edit menu · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/cms/menu-form').then((m) => m.AdminMenuForm),
      },
      {
        path: 'news',
        title: 'News · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/cms/news').then((m) => m.AdminNews),
      },
      {
        path: 'news/:id',
        title: 'Edit article · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/cms/news-form').then((m) => m.AdminNewsForm),
      },
      {
        path: 'content-blocks',
        title: 'Content blocks · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/content-blocks/content-blocks').then(
            (m) => m.AdminContentBlocks,
          ),
      },
      {
        path: 'content-blocks/:id',
        title: 'Edit content block · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/content-blocks/content-block-form').then(
            (m) => m.AdminContentBlockForm,
          ),
      },
      {
        path: 'promotions',
        title: 'Promotions · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/promotions/promotions').then(
            (m) => m.AdminPromotions,
          ),
      },
      {
        path: 'promotions/:id',
        title: 'Edit promotion · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/promotions/promotion-form').then(
            (m) => m.AdminPromotionForm,
          ),
      },
      {
        path: 'warehouses',
        title: 'Warehouses · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/warehouses/warehouses').then(
            (m) => m.AdminWarehouses,
          ),
      },
      {
        path: 'warehouses/:id',
        title: 'Edit warehouse · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/warehouses/warehouse-form').then(
            (m) => m.AdminWarehouseForm,
          ),
      },
      {
        path: 'taxes',
        title: 'Taxes · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/tax/taxes').then((m) => m.AdminTaxes),
      },
      {
        path: 'taxes/:id',
        title: 'Edit tax rate · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/tax/tax-rate-form').then((m) => m.AdminTaxRateForm),
      },
      {
        path: 'shipping',
        title: 'Shipping · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/shipping/shipping').then((m) => m.AdminShipping),
      },
      {
        path: 'shipping/:id',
        title: 'Edit table rate · MadeWithDetermination Admin',
        loadComponent: () =>
          import('./features/shipping/table-rate-form').then(
            (m) => m.AdminTableRateForm,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
