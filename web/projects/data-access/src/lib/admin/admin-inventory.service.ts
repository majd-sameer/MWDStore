import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type { ProductStockDto, StockAdjustmentRequest } from '../models';

/** Admin inventory (`/api/admin/inventory`). */
@Injectable({ providedIn: 'root' })
export class AdminInventoryService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/inventory/products/{productId} — skipped while no product is selected. */
  productStockResource(productId: () => number | null) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ProductStockDto>(() => {
        const id = productId();
        return id ? `${API_ROOT}/admin/inventory/products/${id}` : undefined;
      }),
    );
  }

  /** POST /api/admin/inventory/adjust */
  adjust(body: StockAdjustmentRequest): Observable<ProductStockDto> {
    return this.http.post<ProductStockDto>(
      `${API_ROOT}/admin/inventory/adjust`,
      body,
    );
  }
}
