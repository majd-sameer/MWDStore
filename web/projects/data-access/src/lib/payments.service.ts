import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from './http-utils';
import type {
  GatewayCallbackRequest,
  GatewayInitiationResult,
  GatewayPaymentResult,
  GuestPaymentInitiateRequest,
  PaymentInitiateRequest,
  PaymentMethodDto,
  StripeVerifyRequest,
} from './models';

/**
 * Storefront payments. The checkout offers whatever payment methods the admin has
 * enabled (`/api/admin/payments/providers`) and drives the shared redirect-gateway
 * flow (Stripe / PayPal / MEPS): `initiate` starts a payment and returns where to
 * send the shopper; `callback` settles it (the gateway, or the sandbox mock page).
 */
@Injectable({ providedIn: 'root' })
export class PaymentsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/payments/methods — the enabled payment methods, as a reactive resource. */
  methodsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<PaymentMethodDto[]>(() => `${API_ROOT}/payments/methods`),
    );
  }

  /** POST /api/payments/initiate — start a gateway payment for an order (auth). */
  initiate(body: PaymentInitiateRequest): Observable<GatewayInitiationResult> {
    return this.http.post<GatewayInitiationResult>(
      `${API_ROOT}/payments/initiate`,
      body,
    );
  }

  /** POST /api/payments/guest/initiate — start a gateway payment for a guest order (validated by email). */
  guestInitiate(body: GuestPaymentInitiateRequest): Observable<GatewayInitiationResult> {
    return this.http.post<GatewayInitiationResult>(
      `${API_ROOT}/payments/guest/initiate`,
      body,
    );
  }

  /** POST /api/payments/callback — settle a gateway payment (anonymous; sandbox mock page). */
  callback(body: GatewayCallbackRequest): Observable<GatewayPaymentResult> {
    return this.http.post<GatewayPaymentResult>(
      `${API_ROOT}/payments/callback`,
      body,
    );
  }

  /** POST /api/payments/stripe/verify — settle a Stripe Checkout payment by session id (anonymous). */
  stripeVerify(body: StripeVerifyRequest): Observable<GatewayPaymentResult> {
    return this.http.post<GatewayPaymentResult>(
      `${API_ROOT}/payments/stripe/verify`,
      body,
    );
  }
}
