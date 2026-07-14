import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from 'core';

export const Role = {
  superAdmin: 'super-admin',
  admin: 'admin',
  salesManager: 'sales-manager',
  sales: 'sales',
  warehouseKeeper: 'warehouse-keeper',
  contentWriter: 'content-writer',
} as const;

export const STAFF_ROLES: readonly string[] = Object.values(Role);


export const AREA = {
  catalog: [Role.superAdmin, Role.admin, Role.warehouseKeeper],
  content: [Role.superAdmin, Role.admin, Role.contentWriter],
  moderation: [Role.superAdmin, Role.admin, Role.contentWriter],
  inventory: [Role.superAdmin, Role.admin, Role.warehouseKeeper],
  fulfillment: [Role.superAdmin, Role.admin, Role.warehouseKeeper],
  sales: [Role.superAdmin, Role.admin, Role.sales, Role.salesManager],
  orders: [Role.superAdmin, Role.admin, Role.sales, Role.salesManager, Role.warehouseKeeper],
  vendors: [Role.superAdmin, Role.admin, Role.sales, Role.salesManager],
  marketing: [Role.superAdmin, Role.admin, Role.salesManager],
  taxes: [Role.superAdmin, Role.admin],
  payments: [Role.superAdmin, Role.admin, Role.salesManager],
  reports: [Role.superAdmin, Role.admin],
  settings: [Role.superAdmin, Role.admin],
  users: [Role.superAdmin, Role.admin],
} as const;


export function adminHomePath(auth: AuthService): string {
  if (auth.hasAnyRole(AREA.reports)) return '/dashboard';
  if (auth.hasAnyRole(AREA.sales)) return '/orders';
  if (auth.hasAnyRole(AREA.inventory)) return '/orders';
  if (auth.hasAnyRole(AREA.catalog)) return '/products';
  if (auth.hasAnyRole(AREA.content)) return '/news';
  if (auth.hasAnyRole(AREA.users)) return '/users';
  if (auth.hasAnyRole(AREA.settings)) return '/settings';
  return '/forbidden';
}


export const adminHomeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth
    .ensureSessionRestored()
    .pipe(map(() => router.parseUrl(adminHomePath(auth))));
};
