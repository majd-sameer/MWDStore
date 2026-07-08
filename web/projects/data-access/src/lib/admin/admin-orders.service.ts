import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { type AdminOrderQuery, API_ROOT, type PagedResult, toQueryParams } from '../http-utils';
import type { OrderDetailDto, OrderSummaryDto, UpdateOrderStatusRequest } from '../models';

/** Admin order management (`/api/admin/orders`). */
@Injectable({ providedIn: 'root' })
export class AdminOrdersService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/orders — paged envelope with total count. */
  listResource(query: () => AdminOrderQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<OrderSummaryDto>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/orders`,
          params: toQueryParams({
            statuses: q.statuses,
            customerId: q.customerId,
            page: q.page,
            pageSize: q.pageSize,
          }),
        };
      }),
    );
  }

  /** GET /api/admin/orders/{id} */
  getResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<OrderDetailDto>(() => `${API_ROOT}/admin/orders/${id()}`),
    );
  }

  /** PUT /api/admin/orders/{id}/status */
  updateStatus(
    id: number,
    body: UpdateOrderStatusRequest,
  ): Observable<OrderDetailDto> {
    return this.http.put<OrderDetailDto>(
      `${API_ROOT}/admin/orders/${id}/status`,
      body,
    );
  }

  /** POST /api/admin/orders/{id}/cancel */
  cancel(id: number): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(
      `${API_ROOT}/admin/orders/${id}/cancel`,
      null,
    );
  }
}
