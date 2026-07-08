import { httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import { API_ROOT, type PagedResult, toQueryParams } from '../http-utils';

/** One row of the audit trail (list projection). */
export interface AuditLogListItem {
  id: number;
  createdOn: string;
  userId: number | null;
  userName: string;
  role: string;
  action: string;
  entityType: string;
  entityId: number | null;
  entityName: string | null;
  area: string;
}

/** Full audit entry including the before/after JSON payloads. */
export interface AuditLogDetail extends AuditLogListItem {
  ipAddress: string | null;
  correlationId: string | null;
  oldValuesJson: string | null;
  newValuesJson: string | null;
}

/** Server-side filters for GET /api/admin/audit-logs. */
export interface AdminAuditQuery {
  from?: string;
  to?: string;
  userId?: number;
  entityType?: string;
  actions?: string[];
  areas?: string[];
  search?: string;
  page?: number;
  pageSize?: number;
}

/** Read-only access to the append-only audit trail (`/api/admin/audit-logs`). */
@Injectable({ providedIn: 'root' })
export class AdminAuditService {
  private readonly injector = inject(Injector);

  /** GET /api/admin/audit-logs */
  listResource(query: () => AdminAuditQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AuditLogListItem>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/audit-logs`,
          params: toQueryParams({
            from: q.from,
            to: q.to,
            userId: q.userId,
            entityType: q.entityType,
            actions: q.actions,
            areas: q.areas,
            search: q.search,
            page: q.page,
            pageSize: q.pageSize,
          }),
        };
      }),
    );
  }

  /** GET /api/admin/audit-logs/{id} — null id skips the request. */
  getResource(id: () => number | null) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AuditLogDetail | undefined>(() => {
        const value = id();
        return value === null
          ? undefined
          : `${API_ROOT}/admin/audit-logs/${value}`;
      }),
    );
  }
}
