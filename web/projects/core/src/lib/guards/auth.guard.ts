import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';

/**
 * Allows activation only for authenticated users; otherwise redirects to the
 * configured login route, preserving the attempted URL as `returnUrl`.
 *
 * Waits for the silent boot restore to settle so a hard refresh (which drops the
 * in-memory access token) recovers the session via the refresh cookie instead of
 * bouncing a still-logged-in user to login.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const { loginUrl } = inject(CORE_CONFIG);

  return auth.ensureSessionRestored().pipe(
    map((authed) =>
      authed
        ? true
        : router.createUrlTree([loginUrl], { queryParams: { returnUrl: state.url } }),
    ),
  );
};
