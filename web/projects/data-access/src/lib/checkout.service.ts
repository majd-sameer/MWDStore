import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from './http-utils';
import type {
  GuestPlaceOrderRequest,
  GuestShippingOptionsRequest,
  OrderDetailDto,
  PlaceOrderRequest,
  ShippingOptionDto,
  ShippingOptionsRequest,
} from './models';

/**
 * Checkout commands. Both endpoints are POSTs (the shipping-options call posts
 * the address to price options), so both use `HttpClient`.
 */
@Injectable({ providedIn: 'root' })
export class CheckoutService {
  private readonly http = inject(HttpClient);

  /** POST /api/checkout/shipping-options */
  shippingOptions(body: ShippingOptionsRequest): Observable<ShippingOptionDto[]> {
    return this.http.post<ShippingOptionDto[]>(
      `${API_ROOT}/checkout/shipping-options`,
      body,
    );
  }

  /** POST /api/checkout/place-order */
  placeOrder(body: PlaceOrderRequest): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(
      `${API_ROOT}/checkout/place-order`,
      body,
    );
  }

  /** POST /api/checkout/guest/shipping-options — shipping options for a guest's posted cart lines. */
  guestShippingOptions(
    body: GuestShippingOptionsRequest,
  ): Observable<ShippingOptionDto[]> {
    return this.http.post<ShippingOptionDto[]>(
      `${API_ROOT}/checkout/guest/shipping-options`,
      body,
    );
  }

  /** POST /api/checkout/guest/place-order — place a guest (no-login) order from posted cart lines. */
  guestPlaceOrder(body: GuestPlaceOrderRequest): Observable<OrderDetailDto> {
    return this.http.post<OrderDetailDto>(
      `${API_ROOT}/checkout/guest/place-order`,
      body,
    );
  }
}
