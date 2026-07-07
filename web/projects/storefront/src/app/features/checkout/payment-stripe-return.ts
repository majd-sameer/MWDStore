import { isPlatformBrowser } from '@angular/common';
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
import { Icon, ToastService } from 'ui';

/**
 * Landing page Stripe Checkout redirects the shopper back to (the session's `success_url`
 * /`cancel_url`, both built server-side at `initiate`). It re-verifies the payment server-side
 * via `/api/payments/stripe/verify` — never trusting the redirect alone — then forwards the
 * shopper to their real destination (`returnUrl`): account for signed-in shoppers, the public
 * track page for guests. The `session_id` is the secret that authorizes settlement.
 */
@Component({
  selector: 'app-payment-stripe-return',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Icon],
  templateUrl: './payment-stripe-return.html',
  styleUrl: './payment-stripe-return.scss',
})
export class PaymentStripeReturn {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly payments = inject(PaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly state = signal<'verifying' | 'paid' | 'failed'>('verifying');

  private readonly params = this.route.snapshot.queryParamMap;
  private readonly sessionId = this.params.get('session_id') ?? '';
  private readonly canceled = this.params.get('canceled') === '1';
  private readonly returnUrl = this.params.get('returnUrl') || '/account';

  constructor() {
    if (!this.isBrowser) {
      return;
    }

    // Abandoned at Stripe's page (cancel_url): the order stays pending so the shopper can retry.
    if (this.canceled || !this.sessionId) {
      this.state.set('failed');
      this.toast.error(this.translate.instant('stripe_return.canceled'));
      this.leaveSoon();
      return;
    }

    this.payments.stripeVerify({ sessionId: this.sessionId }).subscribe({
      next: (res) => {
        if (res.approved) {
          this.state.set('paid');
          this.toast.success(this.translate.instant('stripe_return.paid'));
        } else {
          this.state.set('failed');
          this.toast.error(this.translate.instant('stripe_return.failed'));
        }
        this.leaveSoon();
      },
      error: () => {
        this.state.set('failed');
        this.toast.error(this.translate.instant('common.error'));
        this.leaveSoon();
      },
    });
  }

  /** Forward to the shopper's destination after a brief beat so the result is visible. */
  private leaveSoon(): void {
    setTimeout(() => {
      if (this.returnUrl.startsWith('/')) {
        void this.router.navigateByUrl(this.returnUrl);
      } else {
        window.location.href = this.returnUrl;
      }
    }, 1200);
  }
}
