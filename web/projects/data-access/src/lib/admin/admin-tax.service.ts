import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type {
  AdminTaxClassDto,
  AdminTaxRateDto,
  TaxClassUpsertRequest,
  TaxRateUpsertRequest,
} from '../models';

/** Admin tax class + tax rate management (`/api/admin/tax`). */
@Injectable({ providedIn: 'root' })
export class AdminTaxService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/tax/classes */
  classesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminTaxClassDto[]>(() => `${API_ROOT}/admin/tax/classes`),
    );
  }

  /** POST /api/admin/tax/classes */
  createClass(body: TaxClassUpsertRequest): Observable<AdminTaxClassDto> {
    return this.http.post<AdminTaxClassDto>(`${API_ROOT}/admin/tax/classes`, body);
  }

  /** PUT /api/admin/tax/classes/{id} */
  updateClass(id: number, body: TaxClassUpsertRequest): Observable<AdminTaxClassDto> {
    return this.http.put<AdminTaxClassDto>(`${API_ROOT}/admin/tax/classes/${id}`, body);
  }

  /** DELETE /api/admin/tax/classes/{id} */
  deleteClass(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/tax/classes/${id}`);
  }

  /** GET /api/admin/tax/rates */
  ratesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminTaxRateDto[]>(() => `${API_ROOT}/admin/tax/rates`),
    );
  }

  /** POST /api/admin/tax/rates */
  createRate(body: TaxRateUpsertRequest): Observable<AdminTaxRateDto> {
    return this.http.post<AdminTaxRateDto>(`${API_ROOT}/admin/tax/rates`, body);
  }

  /** PUT /api/admin/tax/rates/{id} */
  updateRate(id: number, body: TaxRateUpsertRequest): Observable<AdminTaxRateDto> {
    return this.http.put<AdminTaxRateDto>(`${API_ROOT}/admin/tax/rates/${id}`, body);
  }

  /** DELETE /api/admin/tax/rates/{id} */
  deleteRate(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/tax/rates/${id}`);
  }
}
