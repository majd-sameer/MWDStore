import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';


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
