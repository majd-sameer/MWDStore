import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from 'core';

/**
 * Back-office role names. These strings must match the API's `AppRoles`
 * (kebab-case) exactly — they travel verbatim in the JWT role claim and are
 * compared by the shared `roleGuard`.
 */
export const Role = {
  superAdmin: 'super-admin',
  admin: 'admin',
  salesManager: 'sales-manager',
  sales: 'sales',
  warehouseKeeper: 'warehouse-keeper',
  contentWriter: 'content-writer',
} as const;

/** Every staff role — anyone who may enter the admin app at all. */
export const STAFF_ROLES: readonly string[] = Object.values(Role);

/**
 * Which roles may reach each area of the console. Mirrors the API's
 * `AuthPolicies`; both must agree or the UI would show links that 403.
 */
export const AREA = {
  catalog: [Role.superAdmin, Role.admin, Role.contentWriter],
  content: [Role.superAdmin, Role.admin, Role.contentWriter],
  moderation: [Role.superAdmin, Role.admin, Role.contentWriter],
  inventory: [Role.superAdmin, Role.admin, Role.warehouseKeeper],
  fulfillment: [Role.superAdmin, Role.admin, Role.warehouseKeeper],
  sales: [Role.superAdmin, Role.admin, Role.sales, Role.salesManager],
  marketing: [Role.superAdmin, Role.admin, Role.salesManager],
  reports: [Role.superAdmin, Role.admin, Role.salesManager],
  settings: [Role.superAdmin, Role.admin],
  users: [Role.superAdmin],
} as const;

/**
 * Picks the landing section for a signed-in user, following the sidebar order:
 * dashboard → orders → stock (inventory) → products → cms → users → settings.
 */
export function adminHomePath(auth: AuthService): string {
  if (auth.hasAnyRole(AREA.reports)) return '/dashboard';
  if (auth.hasAnyRole(AREA.sales)) return '/orders';
  if (auth.hasAnyRole(AREA.inventory)) return '/inventory';
  if (auth.hasAnyRole(AREA.catalog)) return '/products';
  if (auth.hasAnyRole(AREA.content)) return '/news';
  if (auth.hasAnyRole(AREA.users)) return '/users';
  if (auth.hasAnyRole(AREA.settings)) return '/settings';
  return '/forbidden';
}

/**
 * Index guard: redirects `/` to the role-appropriate home. A warehouse keeper
 * has no dashboard access, so sending everyone to the dashboard would bounce
 * them to `forbidden`; instead each role lands on its first reachable section.
 */
export const adminHomeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth
    .ensureSessionRestored()
    .pipe(map(() => router.parseUrl(adminHomePath(auth))));
};
