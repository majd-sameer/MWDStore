import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type {
  AdminProductOptionListItem,
  ProductOptionUpsertRequest,
} from '../models';

/** Admin product option management (`/api/admin/product-options`). */
@Injectable({ providedIn: 'root' })
export class AdminProductOptionsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/product-options */
  listResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminProductOptionListItem[]>(
        () => `${API_ROOT}/admin/product-options`,
      ),
    );
  }

  /** POST /api/admin/product-options */
  create(body: ProductOptionUpsertRequest): Observable<AdminProductOptionListItem> {
    return this.http.post<AdminProductOptionListItem>(
      `${API_ROOT}/admin/product-options`,
      body,
    );
  }

  /** PUT /api/admin/product-options/{id} */
  update(
    id: number,
    body: ProductOptionUpsertRequest,
  ): Observable<AdminProductOptionListItem> {
    return this.http.put<AdminProductOptionListItem>(
      `${API_ROOT}/admin/product-options/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/product-options/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/product-options/${id}`);
  }
}
