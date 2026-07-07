import { isPlatformBrowser } from '@angular/common';
import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PaymentsService } from 'data-access';
import { Button, Icon, ToastService } from 'ui';

/**
 * Sandbox-only simulation of a gateway hosted payment page. The real Stripe/PayPal/MEPS
 * integrations are stubs, so in sandbox the checkout sends the shopper here instead of an
 * external URL. Approve/Decline post to the same `/api/payments/callback` a live gateway
 * would hit, then we return the shopper to their account. Not for production: when a gateway
 * is wired for real, `initiate` returns a non-sandbox redirect and this page is bypassed.
 */
@Component({
  selector: 'app-payment-gateway-mock',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, TranslatePipe, Button, Icon],
  templateUrl: './payment-gateway-mock.html',
  styleUrl: './payment-gateway-mock.scss',
})
export class PaymentGatewayMock {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly payments = inject(PaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly params = this.route.snapshot.queryParamMap;
  protected readonly orderId = signal(Number(this.params.get('orderId') ?? 0));
  protected readonly paymentId = signal(Number(this.params.get('paymentId') ?? 0));
  protected readonly method = signal(this.params.get('method') ?? '');
  protected readonly amount = signal(Number(this.params.get('amount') ?? 0));
  private readonly returnUrl = this.params.get('returnUrl') ?? '/account';
  protected readonly busy = signal(false);

  protected settle(approve: boolean): void {
    if (this.busy() || !this.orderId()) {
      return;
    }
    this.busy.set(true);
    this.payments
      .callback({
        orderId: this.orderId(),
        method: this.method(),
        result: approve ? 'APPROVED' : 'DECLINED',
        gatewayTransactionId: `SANDBOX-${this.paymentId() || Date.now()}`,
      })
      .subscribe({
        next: (res) => {
          if (res.approved) {
            this.toast.success(this.translate.instant('gateway.paid'));
          } else {
            this.toast.error(this.translate.instant('gateway.declined'));
          }
          this.leave();
        },
        error: () => {
          this.busy.set(false);
          this.toast.error(this.translate.instant('common.error'));
        },
      });
  }

  private leave(): void {
    // returnUrl is an in-app path in sandbox; fall back to a hard redirect for absolute URLs.
    if (this.returnUrl.startsWith('/')) {
      void this.router.navigateByUrl(this.returnUrl);
    } else if (this.isBrowser) {
      window.location.href = this.returnUrl;
    }
  }
}
