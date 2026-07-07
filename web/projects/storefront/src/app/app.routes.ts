import { Routes } from '@angular/router';
import { authGuard } from 'core';

/**
 * Every feature is lazy-loaded. Catalog routes (home / shop / product detail)
 * are public and server-rendered for SEO; cart, auth, checkout and account are
 * user-specific and run client-side only (see `app.routes.server.ts`), so no
 * credentialed data is ever fetched during SSR.
 */
export const routes: Routes = [
  {
    // Catalog routes set their own SEO title/meta via SeoService, so they
    // deliberately omit a static route `title` (which would override it).
    path: '',
    loadComponent: () =>
      import('./features/home/home').then((m) => m.Home),
  },
  {
    // Store Sections hub — pick a category first, then land on /shop?category=…
    path: 'categories',
    loadComponent: () =>
      import('./features/catalog/category-list').then((m) => m.CategoryList),
  },
  {
    path: 'shop',
    loadComponent: () =>
      import('./features/catalog/product-list').then((m) => m.ProductList),
  },
  {
    path: 'products/:id',
    loadComponent: () =>
      import('./features/catalog/product-detail').then((m) => m.ProductDetail),
  },
  {
    // Designed About page (translated copy, no CMS body) — must precede the
    // generic `pages/:slug` matcher. SEO title is owned by the component so
    // it can follow the active language.
    path: 'pages/about-us',
    loadComponent: () =>
      import('./features/content/about').then((m) => m.About),
  },
  {
    path: 'pages/:slug',
    loadComponent: () =>
      import('./features/content/page').then((m) => m.CmsPage),
  },
  {
    path: 'news',
    loadComponent: () =>
      import('./features/content/news-list').then((m) => m.NewsList),
  },
  {
    path: 'news/:slug',
    loadComponent: () =>
      import('./features/content/news-detail').then((m) => m.NewsDetail),
  },
  {
    path: 'contact',
    title: 'Contact us',
    loadComponent: () =>
      import('./features/content/contact').then((m) => m.Contact),
  },
  {
    path: 'compare',
    title: 'Compare products',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/catalog/compare').then((m) => m.Compare),
  },
  {
    path: 'account/wishlist',
    title: 'Wishlist',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/wishlist').then((m) => m.Wishlist),
  },
  {
    // Cart, checkout and confirmation are one screen (an internal stage machine);
    // /cart simply opens it on the cart stage. See features/checkout/checkout.ts.
    path: 'cart',
    title: 'Your cart',
    loadComponent: () =>
      import('./features/checkout/checkout').then((m) => m.Checkout),
  },
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () =>
      import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Create account',
    loadComponent: () =>
      import('./features/auth/register').then((m) => m.Register),
  },
  {
    // Open to everyone: guests check out without an account (the cart lives
    // client-side for anonymous visitors) and track via a 6-digit code + email.
    path: 'checkout',
    title: 'Checkout',
    loadComponent: () =>
      import('./features/checkout/checkout').then((m) => m.Checkout),
  },
  {
    path: 'order-confirmation/:id',
    title: 'Order confirmed',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/checkout/order-confirmation').then(
        (m) => m.OrderConfirmation,
      ),
  },
  {
    // Sandbox-only mock of a gateway hosted page (Stripe/PayPal/MEPS stubs). Reached
    // from checkout after `initiate`; settles via /api/payments/callback. Open to
    // guests too, who pay before returning to the public track page.
    path: 'payment/mock',
    title: 'Complete payment',
    loadComponent: () =>
      import('./features/checkout/payment-gateway-mock').then(
        (m) => m.PaymentGatewayMock,
      ),
  },
  {
    // Stripe Checkout return landing (success_url / cancel_url). Verifies the payment
    // server-side, then forwards the shopper to their account or the public track page.
    path: 'payment/stripe/return',
    title: 'Confirming payment',
    loadComponent: () =>
      import('./features/checkout/payment-stripe-return').then(
        (m) => m.PaymentStripeReturn,
      ),
  },
  {
    // Public order tracking by number + email (no sign-in required).
    path: 'track-order',
    title: 'Track your order',
    loadComponent: () =>
      import('./features/order-tracking/order-tracking').then(
        (m) => m.OrderTracking,
      ),
  },
  {
    path: 'account',
    title: 'My account',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/account').then((m) => m.Account),
  },
  {
    path: 'account/orders',
    title: 'Order history',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/order-history').then((m) => m.OrderHistory),
  },
  {
    path: 'account/orders/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/order-detail').then((m) => m.OrderDetail),
  },
  { path: '**', redirectTo: '' },
];
