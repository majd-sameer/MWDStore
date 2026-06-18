import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
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

  /** GET /api/admin/payments */
  paymentsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminPaymentDto[]>(() => `${API_ROOT}/admin/payments`),
    );
  }
}
