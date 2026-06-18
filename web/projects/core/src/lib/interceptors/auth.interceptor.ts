import type { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';

/** True when the request targets our own API (relative `/api` or our origin). */
function isApiRequest(req: HttpRequest<unknown>, apiBaseUrl: string): boolean {
  if (req.url.startsWith('/api')) {
    return true;
  }
  return apiBaseUrl !== '' && req.url.startsWith(`${apiBaseUrl}/api`);
}

/**
 * Attaches `Authorization: Bearer <token>` from the in-memory access token.
 * The bearer is only added to first-party API requests so it never leaks to a
 * third-party host.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const { apiBaseUrl } = inject(CORE_CONFIG);
  const token = auth.accessToken();

  if (token && isApiRequest(req, apiBaseUrl)) {
    return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  }
  return next(req);
};
