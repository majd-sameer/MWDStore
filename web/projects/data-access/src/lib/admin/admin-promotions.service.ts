import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type {
  AdminCartRuleDetail,
  AdminCartRuleListItem,
  AdminCartRuleUsageDto,
  CartRuleUpsertRequest,
} from '../models';

/** Admin promotions (cart rules + coupons) management (`/api/admin/promotions`). */
@Injectable({ providedIn: 'root' })
export class AdminPromotionsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/promotions */
  listResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCartRuleListItem[]>(() => `${API_ROOT}/admin/promotions`),
    );
  }

  /** GET /api/admin/promotions/{id} */
  get(id: number): Observable<AdminCartRuleDetail> {
    return this.http.get<AdminCartRuleDetail>(`${API_ROOT}/admin/promotions/${id}`);
  }

  /** POST /api/admin/promotions */
  create(body: CartRuleUpsertRequest): Observable<AdminCartRuleDetail> {
    return this.http.post<AdminCartRuleDetail>(`${API_ROOT}/admin/promotions`, body);
  }

  /** PUT /api/admin/promotions/{id} */
  update(id: number, body: CartRuleUpsertRequest): Observable<AdminCartRuleDetail> {
    return this.http.put<AdminCartRuleDetail>(
      `${API_ROOT}/admin/promotions/${id}`,
      body,
    );
  }

  /** DELETE /api/admin/promotions/{id} */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/promotions/${id}`);
  }

  /** GET /api/admin/promotions/usages */
  usages(cartRuleId?: number): Observable<AdminCartRuleUsageDto[]> {
    return this.http.get<AdminCartRuleUsageDto[]>(
      `${API_ROOT}/admin/promotions/usages`,
      { params: toQueryParams({ cartRuleId }) },
    );
  }
}
