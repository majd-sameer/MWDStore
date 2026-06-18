import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type { AdminCategoryDto, CategoryUpsertRequest } from '../models';

/** Admin category management (`/api/admin/categories`). */
@Injectable({ providedIn: 'root' })
export class AdminCategoriesService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/categories */
  listResource(includeDeleted: () => boolean = () => false) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCategoryDto[]>(() => ({
        url: `${API_ROOT}/admin/categories`,
        params: toQueryParams({ includeDeleted: includeDeleted() }),
      })),
    );
  }

  /** GET /api/admin/categories/{id} */
  getResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCategoryDto>(
        () => `${API_ROOT}/admin/categories/${id()}`,
      ),
    );
  }

  /** POST /api/admin/categories */
  create(body: CategoryUpsertRequest): Observable<AdminCategoryDto> {
    return this.http.post<AdminCategoryDto>(
      `${API_ROOT}/admin/categories`,
      body,
    );
  }

  /** PUT /api/admin/categories/{id} */
  update(id: number, body: CategoryUpsertRequest): Observable<AdminCategoryDto> {
    return this.http.put<AdminCategoryDto>(
      `${API_ROOT}/admin/categories/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/categories/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/categories/${id}`);
  }
}
