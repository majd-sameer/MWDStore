import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type {
  AdminProductAttributeDto,
  AdminProductAttributeGroupDto,
  ProductAttributeGroupUpsertRequest,
  ProductAttributeUpsertRequest,
} from '../models';

/** Admin product attribute + attribute-group management (`/api/admin/product-attributes`). */
@Injectable({ providedIn: 'root' })
export class AdminProductAttributesService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/product-attributes */
  listResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminProductAttributeDto[]>(
        () => `${API_ROOT}/admin/product-attributes`,
      ),
    );
  }

  /** POST /api/admin/product-attributes */
  create(body: ProductAttributeUpsertRequest): Observable<AdminProductAttributeDto> {
    return this.http.post<AdminProductAttributeDto>(
      `${API_ROOT}/admin/product-attributes`,
      body,
    );
  }

  /** PUT /api/admin/product-attributes/{id} */
  update(
    id: number,
    body: ProductAttributeUpsertRequest,
  ): Observable<AdminProductAttributeDto> {
    return this.http.put<AdminProductAttributeDto>(
      `${API_ROOT}/admin/product-attributes/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/product-attributes/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/product-attributes/${id}`);
  }

  /** GET /api/admin/product-attributes/groups */
  groupsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminProductAttributeGroupDto[]>(
        () => `${API_ROOT}/admin/product-attributes/groups`,
      ),
    );
  }

  /** POST /api/admin/product-attributes/groups */
  createGroup(
    body: ProductAttributeGroupUpsertRequest,
  ): Observable<AdminProductAttributeGroupDto> {
    return this.http.post<AdminProductAttributeGroupDto>(
      `${API_ROOT}/admin/product-attributes/groups`,
      body,
    );
  }

  /** PUT /api/admin/product-attributes/groups/{id} */
  updateGroup(
    id: number,
    body: ProductAttributeGroupUpsertRequest,
  ): Observable<AdminProductAttributeGroupDto> {
    return this.http.put<AdminProductAttributeGroupDto>(
      `${API_ROOT}/admin/product-attributes/groups/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/product-attributes/groups/{id} */
  deleteGroup(id: number): Observable<void> {
    return this.http.delete<void>(
      `${API_ROOT}/admin/product-attributes/groups/${id}`,
    );
  }
}
