import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, type PagedResult, toQueryParams } from '../http-utils';
import type {
  AdminCustomerGroupDto,
  AdminUserCreateRequest,
  AdminUserDetail,
  AdminUserListItem,
  AdminUserUpdateRequest,
  CustomerGroupUpsertRequest,
  RoleDto,
} from '../models';

/** Admin user + customer-group management (`/api/admin/users`, `/api/admin/customer-groups`). */
@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/users — paged envelope with total count. */
  listResource(
    query: () => {
      query?: string;
      role?: string;
      includeDeleted?: boolean;
      page?: number;
      pageSize?: number;
    } = () => ({}),
  ) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AdminUserListItem>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/users`,
          params: toQueryParams({
            query: q.query,
            role: q.role,
            includeDeleted: q.includeDeleted,
            page: q.page,
            pageSize: q.pageSize,
          }),
        };
      }),
    );
  }

  /** GET /api/admin/users/roles */
  rolesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<RoleDto[]>(() => `${API_ROOT}/admin/users/roles`),
    );
  }

  /** GET /api/admin/users/{id} */
  get(id: number): Observable<AdminUserDetail> {
    return this.http.get<AdminUserDetail>(`${API_ROOT}/admin/users/${id}`);
  }

  /** POST /api/admin/users */
  create(body: AdminUserCreateRequest): Observable<AdminUserDetail> {
    return this.http.post<AdminUserDetail>(`${API_ROOT}/admin/users`, body);
  }

  /** PUT /api/admin/users/{id} */
  update(id: number, body: AdminUserUpdateRequest): Observable<AdminUserDetail> {
    return this.http.put<AdminUserDetail>(`${API_ROOT}/admin/users/${id}`, body);
  }

  /** DELETE /api/admin/users/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/users/${id}`);
  }

  // ----- Customer groups ------------------------------------------------------

  /** GET /api/admin/customer-groups */
  groupsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCustomerGroupDto[]>(
        () => `${API_ROOT}/admin/customer-groups`,
      ),
    );
  }

  /** POST /api/admin/customer-groups */
  createGroup(body: CustomerGroupUpsertRequest): Observable<AdminCustomerGroupDto> {
    return this.http.post<AdminCustomerGroupDto>(
      `${API_ROOT}/admin/customer-groups`,
      body,
    );
  }

  /** PUT /api/admin/customer-groups/{id} */
  updateGroup(
    id: number,
    body: CustomerGroupUpsertRequest,
  ): Observable<AdminCustomerGroupDto> {
    return this.http.put<AdminCustomerGroupDto>(
      `${API_ROOT}/admin/customer-groups/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/customer-groups/{id} */
  deleteGroup(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/customer-groups/${id}`);
  }
}
