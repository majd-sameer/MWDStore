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
