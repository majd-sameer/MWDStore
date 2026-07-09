import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type { AdminWarehouseDto, WarehouseUpsertRequest } from '../models';

/** Admin warehouse management (`/api/admin/warehouses`). */
@Injectable({ providedIn: 'root' })
export class AdminWarehousesService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /**
   * GET /api/admin/warehouses. Pass `enabled` to gate the request reactively —
   * when it returns false the resource stays idle (no fetch), so callers without
   * inventory access don't trigger a 403. Defaults to always-on.
   */
  listResource(enabled?: () => boolean) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminWarehouseDto[]>(() =>
        enabled && !enabled() ? undefined : `${API_ROOT}/admin/warehouses`,
      ),
    );
  }

  /** POST /api/admin/warehouses */
  create(body: WarehouseUpsertRequest): Observable<AdminWarehouseDto> {
    return this.http.post<AdminWarehouseDto>(`${API_ROOT}/admin/warehouses`, body);
  }

  /** PUT /api/admin/warehouses/{id} */
  update(id: number, body: WarehouseUpsertRequest): Observable<AdminWarehouseDto> {
    return this.http.put<AdminWarehouseDto>(
      `${API_ROOT}/admin/warehouses/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/warehouses/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/warehouses/${id}`);
  }
}
