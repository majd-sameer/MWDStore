import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from './http-utils';
import type { OrderDetailDto, OrderSummaryDto, OrderTrackingDto } from './models';

/** Current customer's order history (auth reads) + public order tracking. */
@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /**
   * GET /api/orders. Pass `enabled` to gate the request (e.g. only when the user
   * is authenticated) — the resource stays idle while it returns `false`.
   */
  ordersResource(enabled: () => boolean = () => true) {
    return runInInjectionContext(this.injector, () =>
      httpResource<OrderSummaryDto[]>(() =>
        enabled() ? `${API_ROOT}/orders` : undefined,
      ),
    );
  }

  /** GET /api/orders/{id}. Stays idle while `id()` is falsy (0/NaN). */
  orderResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<OrderDetailDto>(() => {
        const value = id();
        return value ? `${API_ROOT}/orders/${value}` : undefined;
      }),
    );
  }

  /** GET /api/orders/track — public lookup of an order's status by its 6-digit tracking number. */
  track(number: string): Observable<OrderTrackingDto> {
    return this.http.get<OrderTrackingDto>(`${API_ROOT}/orders/track`, {
      params: toQueryParams({ number }),
    });
  }
}
