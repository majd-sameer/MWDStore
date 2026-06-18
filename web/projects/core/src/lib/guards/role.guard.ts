import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';

/**
 * Builds a guard that requires the user to hold at least one of `roles`
 * (e.g. `roleGuard('Admin')` for admin-only routes). Unauthenticated users are
 * sent to login; authenticated users lacking the role are sent to the
 * forbidden route.
 *
 * Waits for the silent boot restore to settle first, so a hard refresh recovers
 * the session via the refresh cookie instead of bouncing the user to login.
 */
export function roleGuard(...roles: string[]): CanActivateFn {
  return (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const { loginUrl, forbiddenUrl } = inject(CORE_CONFIG);

    return auth.ensureSessionRestored().pipe(
      map((authed) => {
        if (!authed) {
          return router.createUrlTree([loginUrl], {
            queryParams: { returnUrl: state.url },
          });
        }
        return auth.hasAnyRole(roles) ? true : router.createUrlTree([forbiddenUrl]);
      }),
    );
  };
}
