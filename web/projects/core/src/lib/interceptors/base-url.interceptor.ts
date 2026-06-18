import type { HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { CORE_CONFIG } from '../config/core-config';

/**
 * Prepends a backend origin to root-relative `/...` requests (the data-access
 * services emit `/api/...`).
 *
 * - **Browser:** uses `apiBaseUrl`. Empty (the default) keeps requests
 *   same-origin — the recommended setup, since it keeps Angular's built-in XSRF
 *   and the httpOnly refresh cookie working without CORS.
 * - **Server (SSR):** uses `ssrApiBaseUrl` (falling back to `apiBaseUrl`),
 *   because the server's `fetch` cannot resolve a relative URL. This only
 *   affects anonymous catalog reads rendered on the server.
 */
export const baseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const { apiBaseUrl, ssrApiBaseUrl } = inject(CORE_CONFIG);
  const onServer = isPlatformServer(inject(PLATFORM_ID));
  const base = onServer ? ssrApiBaseUrl || apiBaseUrl : apiBaseUrl;

  if (base && req.url.startsWith('/')) {
    return next(req.clone({ url: `${base}${req.url}` }));
  }
  return next(req);
};
