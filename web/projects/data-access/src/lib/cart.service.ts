import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from './http-utils';
import type {
  AddToCartRequest,
  CartModel,
  UpdateCartItemRequest,
} from './models';

/** Shopping cart: a GET read plus add/update/remove commands. */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /**
   * GET /api/cart. `enabled` gates the request: while it returns `false` (e.g.
   * the visitor is not authenticated) the resource makes no call, so guests
   * never trigger a 401 on the protected cart endpoint.
   */
  cartResource(
    couponCode: () => string | undefined = () => undefined,
    enabled: () => boolean = () => true,
  ) {
    return runInInjectionContext(this.injector, () =>
      httpResource<CartModel>(() =>
        enabled()
          ? {
              url: `${API_ROOT}/cart`,
              params: toQueryParams({ couponCode: couponCode() }),
            }
          : undefined,
      ),
    );
  }

  /** POST /api/cart/items */
  addItem(body: AddToCartRequest): Observable<CartModel> {
    return this.http.post<CartModel>(`${API_ROOT}/cart/items`, body);
  }

  /** PUT /api/cart/items/{cartItemId} */
  updateItem(
    cartItemId: number,
    body: UpdateCartItemRequest,
  ): Observable<CartModel> {
    return this.http.put<CartModel>(
      `${API_ROOT}/cart/items/${cartItemId}`,
      body,
    );
  }

  /** DELETE /api/cart/items/{cartItemId} */
  removeItem(cartItemId: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/cart/items/${cartItemId}`);
  }
}
