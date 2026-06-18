import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type { AdminBrandDto, BrandUpsertRequest } from '../models';

/** Admin brand management (`/api/admin/brands`). */
@Injectable({ providedIn: 'root' })
export class AdminBrandsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/brands */
  listResource(includeDeleted: () => boolean = () => false) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminBrandDto[]>(() => ({
        url: `${API_ROOT}/admin/brands`,
        params: toQueryParams({ includeDeleted: includeDeleted() }),
      })),
    );
  }

  /** GET /api/admin/brands/{id} */
  getResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminBrandDto>(() => `${API_ROOT}/admin/brands/${id()}`),
    );
  }

  /** POST /api/admin/brands */
  create(body: BrandUpsertRequest): Observable<AdminBrandDto> {
    return this.http.post<AdminBrandDto>(`${API_ROOT}/admin/brands`, body);
  }

  /** PUT /api/admin/brands/{id} */
  update(id: number, body: BrandUpsertRequest): Observable<AdminBrandDto> {
    return this.http.put<AdminBrandDto>(`${API_ROOT}/admin/brands/${id}`, body);
  }

  /** DELETE /api/admin/brands/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/brands/${id}`);
  }
}
