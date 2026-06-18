import { httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import { API_ROOT } from '../http-utils';
import type { AdminDashboardDto } from '../models';

/** Admin dashboard analytics (`/api/admin/dashboard`). */
@Injectable({ providedIn: 'root' })
export class AdminDashboardService {
  private readonly injector = inject(Injector);

  /** GET /api/admin/dashboard/stats — aggregates over the last `days` days (default 30). */
  statsResource(days: () => number = () => 30) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminDashboardDto>(() => ({
        url: `${API_ROOT}/admin/dashboard/stats`,
        params: { days: days() },
      })),
    );
  }
}
