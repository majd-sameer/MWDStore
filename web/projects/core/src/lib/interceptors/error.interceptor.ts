import {
  type HttpErrorResponse,
  type HttpInterceptorFn,
  type HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';
import { SKIP_SESSION_REDIRECT } from './skip-session-redirect';

function urlContains(req: HttpRequest<unknown>, path: string): boolean {
  return req.url.includes(path);
}

/**
 * Centralizes HTTP failure handling:
 *
 * - **401** on a protected resource → silent refresh once (rotating the
 *   refresh cookie), then retry the original request with the new bearer. If
 *   the refresh or the retry fails, clear the session and redirect to login.
 *   Failures on the auth endpoints themselves are passed through (login errors)
 *   or treated as a dead session (refresh endpoint).
 * - **403** → redirect to the forbidden route.
 * - **5xx** → logged with the correlation id for tracing, then re-thrown.
 *
 * Because this interceptor is registered last, its `next()` is the backend
 * handler — the retried request bypasses the chain (so there's no refresh
 * loop), which is why the new bearer is re-attached here explicitly.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const config = inject(CORE_CONFIG);

  const failSession = (error: unknown) => {
    auth.clearSession();
    void router.navigate([config.loginUrl]);
    return throwError(() => error);
  };

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Caller opted out of the login bounce (e.g. silent boot restore for a
        // guest): clear any stale session, but stay put and let them continue.
        if (req.context.get(SKIP_SESSION_REDIRECT)) {
          auth.clearSession();
          return throwError(() => error);
        }
        // The refresh endpoint itself failing means the session is dead.
        if (urlContains(req, config.refreshPath)) {
          return failSession(error);
        }
        // Login/register failures belong to the caller (e.g. bad credentials).
        if (urlContains(req, '/api/auth/')) {
          return throwError(() => error);
        }
        // Protected resource: refresh once, then retry with the fresh token.
        return auth.refresh().pipe(
          switchMap((token) =>
            next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })),
          ),
          catchError((retryError) => failSession(retryError)),
        );
      }

      if (error.status === 403) {
        void router.navigate([config.forbiddenUrl]);
      } else if (error.status >= 500) {
        const correlationId = req.headers.get(config.correlationIdHeader);
        console.error(
          `[${correlationId ?? 'n/a'}] ${req.method} ${req.url} → ${error.status} ${error.statusText}`,
        );
      }

      return throwError(() => error);
    }),
  );
};
