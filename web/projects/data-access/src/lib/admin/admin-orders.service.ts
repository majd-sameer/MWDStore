import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { type AdminOrderQuery, API_ROOT, toQueryParams } from '../http-utils';
import type {
  OrderDetailDto,
  OrderSummaryDto,
  RefundOrderRequest,
  RefundResultDto,
  UpdateOrderStatusRequest,
} from '../models';

/** Admin order management (`/api/admin/orders`). */
@Injectable({ providedIn: 'root' })
export class AdminOrdersService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/orders */
  listResource(query: () => AdminOrderQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<OrderSummaryDto[]>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/orders`,
          params: toQueryParams({
            status: q.status,
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

  /** POST /api/admin/orders/{id}/refund — full (no amount) or partial refund; idempotent per key. */
  refund(id: number, body: RefundOrderRequest): Observable<RefundResultDto> {
    return this.http.post<RefundResultDto>(
      `${API_ROOT}/admin/orders/${id}/refund`,
      body,
    );
  }
}
