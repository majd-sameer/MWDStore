import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, type PagedResult, toQueryParams } from '../http-utils';
import type {
  AdminPaymentDto,
  AdminPaymentProviderDto,
  PaymentProviderUpdateRequest,
} from '../models';

/** Admin payment providers + transaction log (`/api/admin/payments`). */
@Injectable({ providedIn: 'root' })
export class AdminPaymentsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/payments/providers */
  providersResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminPaymentProviderDto[]>(
        () => `${API_ROOT}/admin/payments/providers`,
      ),
    );
  }

  /** PUT /api/admin/payments/providers/{id} */
  updateProvider(
    id: string,
    body: PaymentProviderUpdateRequest,
  ): Observable<AdminPaymentProviderDto> {
    return this.http.put<AdminPaymentProviderDto>(
      `${API_ROOT}/admin/payments/providers/${id}`,
      body,
    );
  }

  /** GET /api/admin/payments — paged envelope with total count. */
  paymentsResource(
    query: () => { page?: number; pageSize?: number } = () => ({}),
  ) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AdminPaymentDto>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/payments`,
          params: toQueryParams({ page: q.page, pageSize: q.pageSize }),
        };
      }),
    );
  }
}
