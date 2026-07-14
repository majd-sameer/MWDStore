import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';


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
