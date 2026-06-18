import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
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

  /** GET /api/admin/customers */
  listResource(query: () => { query?: string; includeDeleted?: boolean } = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCustomerListItem[]>(() => ({
        url: `${API_ROOT}/admin/customers`,
        params: toQueryParams({
          query: query().query,
          includeDeleted: query().includeDeleted,
        }),
      })),
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
