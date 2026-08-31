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
 * Landing page for a MadfoatCom (PayTabs) payment.
 *
 * PayTabs form-POSTs the shopper's browser to the API's `paytabs/return`, which no SPA route could
 * accept, so the API verifies that POST and 302s here with the `tranRef` on the query string. This
 * page then asks the server to settle via `/api/payments/paytabs/verify` — which re-queries PayTabs
 * for the authoritative status rather than trusting anything the browser carried — and forwards the
 * shopper to their real destination (`returnUrl`): their account when signed in, the public track
 * page when they checked out as a guest.
 */
@Component({
  selector: 'app-payment-madfoatcom-return',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Icon],
  templateUrl: './payment-madfoatcom-return.html',
  styleUrl: './payment-madfoatcom-return.scss',
})
export class PaymentMadfoatcomReturn {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly payments = inject(PaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly state = signal<'verifying' | 'paid' | 'failed'>('verifying');

  private readonly params = this.route.snapshot.queryParamMap;
  private readonly tranRef = this.params.get('tranRef') ?? '';
  private readonly returnUrl = this.params.get('returnUrl') || '/account';

  constructor() {
    if (!this.isBrowser) {
      return;
    }

    // No reference means the shopper never got as far as a transaction (or came back the long way
    // round). The order stays pending so they can retry from their account.
    if (!this.tranRef) {
      this.state.set('failed');
      this.toast.error(this.translate.instant('madfoatcom_return.canceled'));
      this.leaveSoon();
      return;
    }

    this.payments.paytabsVerify({ tranRef: this.tranRef }).subscribe({
      next: (res) => {
        if (res.approved) {
          this.state.set('paid');
          this.toast.success(this.translate.instant('madfoatcom_return.paid'));
        } else {
          // Also covers PayTabs' pending states — the server left the order payable on purpose.
          this.state.set('failed');
          this.toast.error(this.translate.instant('madfoatcom_return.failed'));
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
