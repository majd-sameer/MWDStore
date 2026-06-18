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
  template: `
    <main class="wrap mock">
      <section class="card">
        <span class="badge"><lib-icon name="lock" [size]="15" /> {{ 'gateway.sandbox' | translate }}</span>
        <h1 class="title">{{ 'gateway.title' | translate: { method: method() } }}</h1>
        <p class="sub">{{ 'gateway.subtitle' | translate }}</p>

        <div class="rows">
          <div class="row">
            <span>{{ 'gateway.order' | translate }}</span>
            <b class="tabular-nums">#{{ orderId() }}</b>
          </div>
          <div class="row">
            <span>{{ 'gateway.method' | translate }}</span>
            <b>{{ method() }}</b>
          </div>
          <div class="row total">
            <span>{{ 'gateway.amount' | translate }}</span>
            <strong class="tabular-nums">{{ amount() | money }}</strong>
          </div>
        </div>

        <div class="actions">
          <button libButton variant="primary" size="lg" [block]="true"
            [disabled]="busy()" (click)="settle(true)">
            <lib-icon name="check" [size]="18" /> {{ 'gateway.approve' | translate }}
          </button>
          <button libButton variant="secondary" [outline]="true" size="lg" [block]="true"
            [disabled]="busy()" (click)="settle(false)">
            {{ 'gateway.decline' | translate }}
          </button>
        </div>

        <p class="note">{{ 'gateway.note' | translate }}</p>
      </section>
    </main>
  `,
  styles: `
    :host {
      display: block;
    }
    .mock {
      padding-block: 60px;
      display: flex;
      justify-content: center;
    }
    .card {
      inline-size: 100%;
      max-inline-size: 460px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 28px;
      text-align: center;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 5px 12px;
      border-radius: 999px;
      background: var(--surface-2);
      color: var(--accent);
      font-size: 0.8rem;
      font-weight: 700;
    }
    .title {
      font-size: 1.4rem;
      font-weight: 700;
      margin-block: 16px 6px;
    }
    .sub {
      color: var(--ink-2);
      margin-block-end: 22px;
    }
    .rows {
      text-align: start;
      border: 1px solid var(--line);
      border-radius: var(--r);
      padding: 6px 18px;
      margin-block-end: 22px;
    }
    .row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding-block: 11px;
      color: var(--ink-2);
      border-block-end: 1px solid var(--line);
    }
    .row:last-child {
      border-block-end: 0;
    }
    .row.total {
      font-size: 1.15rem;
      font-weight: 700;
      color: var(--ink);
    }
    .actions {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .note {
      margin-block-start: 18px;
      font-size: 0.82rem;
      color: var(--ink-3);
    }
  `,
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
