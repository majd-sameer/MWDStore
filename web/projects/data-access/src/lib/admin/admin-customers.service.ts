import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, type PagedResult, toQueryParams } from '../http-utils';
import type {
  AdminCustomerCreateRequest,
  AdminCustomerDetail,
  AdminCustomerListItem,
  AdminCustomerGroupDto,
  AdminCustomerUpdateRequest,
} from '../models';

/** Admin customer directory (`/api/admin/customers`) — storefront shoppers (non-admin users). */
@Injectable({ providedIn: 'root' })
export class AdminCustomersService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/customers — paged envelope with total count. */
  listResource(
    query: () => {
      query?: string;
      includeDeleted?: boolean;
      page?: number;
      pageSize?: number;
    } = () => ({}),
  ) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AdminCustomerListItem>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/customers`,
          params: toQueryParams({
            query: q.query,
            includeDeleted: q.includeDeleted,
            page: q.page,
            pageSize: q.pageSize,
          }),
        };
      }),
    );
  }

  /** GET /api/admin/customer-groups (shared with the user admin). */
  groupsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCustomerGroupDto[]>(
        () => `${API_ROOT}/admin/customer-groups`,
      ),
    );
  }

  /** GET /api/admin/customers/{id} */
  get(id: number): Observable<AdminCustomerDetail> {
    return this.http.get<AdminCustomerDetail>(`${API_ROOT}/admin/customers/${id}`);
  }

  /** POST /api/admin/customers */
  create(body: AdminCustomerCreateRequest): Observable<AdminCustomerDetail> {
    return this.http.post<AdminCustomerDetail>(`${API_ROOT}/admin/customers`, body);
  }

  /** PUT /api/admin/customers/{id} */
  update(id: number, body: AdminCustomerUpdateRequest): Observable<AdminCustomerDetail> {
    return this.http.put<AdminCustomerDetail>(`${API_ROOT}/admin/customers/${id}`, body);
  }

  /** DELETE /api/admin/customers/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/customers/${id}`);
  }
}
