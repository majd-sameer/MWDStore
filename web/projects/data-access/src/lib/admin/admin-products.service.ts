import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { type AdminProductQuery, API_ROOT, toQueryParams } from '../http-utils';
import type {
  AdminProductDetail,
  AdminProductListItem,
  ProductQuickSearchItem,
  ProductUpsertRequest,
} from '../models';

/** Admin product management (`/api/admin/products`). */
@Injectable({ providedIn: 'root' })
export class AdminProductsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/products */
  listResource(query: () => AdminProductQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminProductListItem[]>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/products`,
          params: toQueryParams({
            query: q.query,
            includeDeleted: q.includeDeleted,
            deletedOnly: q.deletedOnly,
            isPublished: q.isPublished,
            brandId: q.brandId,
            categoryId: q.categoryId,
            page: q.page,
            pageSize: q.pageSize,
          }),
        };
      }),
    );
  }

  /** GET /api/admin/products/{id} */
  getResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminProductDetail>(
        () => `${API_ROOT}/admin/products/${id()}`,
      ),
    );
  }

  /** POST /api/admin/products */
  create(body: ProductUpsertRequest): Observable<AdminProductDetail> {
    return this.http.post<AdminProductDetail>(
      `${API_ROOT}/admin/products`,
      body,
    );
  }

  /** PUT /api/admin/products/{id} */
  update(id: number, body: ProductUpsertRequest): Observable<AdminProductDetail> {
    return this.http.put<AdminProductDetail>(
      `${API_ROOT}/admin/products/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/products/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/products/${id}`);
  }

  /** POST /api/admin/products/{id}/restore — un-deletes the product and its variation children. */
  restore(id: number): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/admin/products/${id}/restore`, null);
  }

  /** GET /api/admin/products/quick-search — picker for related/cross-sell products. */
  quickSearch(query: string): Observable<ProductQuickSearchItem[]> {
    return this.http.get<ProductQuickSearchItem[]>(
      `${API_ROOT}/admin/products/quick-search`,
      { params: toQueryParams({ query }) },
    );
  }
}
