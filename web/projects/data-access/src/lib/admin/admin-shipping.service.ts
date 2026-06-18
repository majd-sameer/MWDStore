import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type {
  AdminShippingProviderDto,
  AdminTableRateDto,
  ShippingProviderUpdateRequest,
  TableRateUpsertRequest,
} from '../models';

/** Admin shipping provider + table-rate management (`/api/admin/shipping`). */
@Injectable({ providedIn: 'root' })
export class AdminShippingService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/shipping/providers */
  providersResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminShippingProviderDto[]>(
        () => `${API_ROOT}/admin/shipping/providers`,
      ),
    );
  }

  /** PUT /api/admin/shipping/providers/{id} */
  updateProvider(
    id: string,
    body: ShippingProviderUpdateRequest,
  ): Observable<AdminShippingProviderDto> {
    return this.http.put<AdminShippingProviderDto>(
      `${API_ROOT}/admin/shipping/providers/${id}`,
      body,
    );
  }

  /** GET /api/admin/shipping/table-rates */
  tableRatesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminTableRateDto[]>(
        () => `${API_ROOT}/admin/shipping/table-rates`,
      ),
    );
  }

  /** POST /api/admin/shipping/table-rates */
  createTableRate(body: TableRateUpsertRequest): Observable<AdminTableRateDto> {
    return this.http.post<AdminTableRateDto>(
      `${API_ROOT}/admin/shipping/table-rates`,
      body,
    );
  }

  /** PUT /api/admin/shipping/table-rates/{id} */
  updateTableRate(
    id: number,
    body: TableRateUpsertRequest,
  ): Observable<AdminTableRateDto> {
    return this.http.put<AdminTableRateDto>(
      `${API_ROOT}/admin/shipping/table-rates/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/shipping/table-rates/{id} */
  deleteTableRate(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/shipping/table-rates/${id}`);
  }
}
