import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type { ProductStockDto, StockAdjustmentRequest } from '../models';

/** StockOutReason enum values (mirror Store.Domain.StockOutReason). */
export const STOCK_OUT_REASON = {
  Sale: 1,
  Gift: 2,
  Matched: 3,
  ThirdParty: 4,
  ExternalEvent: 5,
  Reserved: 6,
  DisplayOnly: 7,
} as const;

/** SalesChannel enum values (mirror Store.Domain.SalesChannel). */
export const SALES_CHANNEL = {
  Showroom: 1,
  ExternalExhibition: 2,
  ExternalBroker: 3,
  LocalBroker: 4,
  OnlineStore: 5,
} as const;

/** Body for POST /api/admin/inventory/stock-out. */
export interface StockOutRequest {
  productId: number;
  warehouseId: number;
  quantity: number;
  reason: number;
  channel?: number | null;
  performedById?: number | null;
  recipientOrRef?: string | null;
  note?: string | null;
}

/** One row of the stock-out log (GET /api/admin/inventory/stock-out-log). */
export interface StockOutLogRow {
  id: number;
  createdOn: string;
  productId: number;
  productName: string | null;
  warehouseId: number;
  warehouseName: string | null;
  quantity: number;
  reason: number | null;
  channel: number | null;
  performedById: number | null;
  performedByName: string | null;
  recipientOrRef: string | null;
  note: string | null;
}

/** Server-side filters for the stock-out log. */
export interface AdminStockOutQuery {
  from?: string;
  to?: string;
  reason?: number;
  channel?: number;
  warehouseId?: number;
  performedById?: number;
  query?: string;
  page?: number;
  pageSize?: number;
}

/** Admin inventory (`/api/admin/inventory`). */
@Injectable({ providedIn: 'root' })
export class AdminInventoryService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/inventory/products/{productId} */
  productStockResource(productId: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ProductStockDto>(
        () => `${API_ROOT}/admin/inventory/products/${productId()}`,
      ),
    );
  }

  /** POST /api/admin/inventory/adjust */
  adjust(body: StockAdjustmentRequest): Observable<ProductStockDto> {
    return this.http.post<ProductStockDto>(
      `${API_ROOT}/admin/inventory/adjust`,
      body,
    );
  }

  /** POST /api/admin/inventory/stock-out */
  stockOut(body: StockOutRequest): Observable<ProductStockDto> {
    return this.http.post<ProductStockDto>(
      `${API_ROOT}/admin/inventory/stock-out`,
      body,
    );
  }

  /** GET /api/admin/inventory/stock-out-log */
  stockOutLogResource(query: () => AdminStockOutQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<StockOutLogRow[]>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/inventory/stock-out-log`,
          params: toQueryParams({
            from: q.from,
            to: q.to,
            reason: q.reason,
            channel: q.channel,
            warehouseId: q.warehouseId,
            performedById: q.performedById,
            query: q.query,
            page: q.page,
            pageSize: q.pageSize,
          }),
        };
      }),
    );
  }
}
