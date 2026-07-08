import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * Render strategy per route.
 *
 * - **Catalog** routes (home, shop, product detail) are public and SEO-relevant
 *   → `RenderMode.Server` (rendered per-request with live data). They issue only
 *   anonymous GETs, so their responses are safely cached/transferred.
 * - **User-specific** routes (cart, auth, checkout, account) are
 *   `RenderMode.Client`: they never execute on the server, so no authenticated
 *   or personal data is ever fetched or serialized into the SSR transfer state.
 */
export const serverRoutes: ServerRoute[] = [
  { path: '', renderMode: RenderMode.Server },
  { path: 'shop', renderMode: RenderMode.Server },
  { path: 'products/:id', renderMode: RenderMode.Server },
  { path: 'pages/about-us', renderMode: RenderMode.Server },
  { path: 'pages/faq', renderMode: RenderMode.Server },
  { path: 'pages/:slug', renderMode: RenderMode.Server },
  { path: 'news', renderMode: RenderMode.Server },
  { path: 'news/:slug', renderMode: RenderMode.Server },

  { path: 'contact', renderMode: RenderMode.Client },
  { path: 'compare', renderMode: RenderMode.Client },
  { path: 'account/wishlist', renderMode: RenderMode.Client },
  { path: 'cart', renderMode: RenderMode.Client },
  { path: 'login', renderMode: RenderMode.Client },
  { path: 'register', renderMode: RenderMode.Client },
  { path: 'checkout', renderMode: RenderMode.Client },
  { path: 'order-confirmation/:id', renderMode: RenderMode.Client },
  { path: 'payment/mock', renderMode: RenderMode.Client },
  { path: 'payment/stripe/return', renderMode: RenderMode.Client },
  { path: 'track-order', renderMode: RenderMode.Client },
  { path: 'account', renderMode: RenderMode.Client },
  { path: 'account/orders', renderMode: RenderMode.Client },
  { path: 'account/orders/:id', renderMode: RenderMode.Client },

  { path: '**', renderMode: RenderMode.Server },
];
