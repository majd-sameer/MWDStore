import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  PLATFORM_ID,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { PaymentsService } from 'data-access';
import { Button, Icon, IconName } from 'ui';

/** What the shopper came back with. `canceled` is a bail-out, `failed` is a gateway "no". */
type ReturnState = 'verifying' | 'paid' | 'canceled' | 'failed';

/** Seconds a settled payment stays on screen before we forward the shopper on. */
const AUTO_FORWARD_SECONDS = 6;

/**
 * Landing page for a MadfoatCom (PayTabs) payment.
 *
 * PayTabs form-POSTs the shopper's browser to the API's `paytabs/return`, which no SPA route could
 * accept, so the API verifies that POST and 302s here with the `tranRef` on the query string. This
 * page then asks the server to settle via `/api/payments/paytabs/verify` — which re-queries PayTabs
 * for the authoritative status rather than trusting anything the browser carried — and forwards the
 * shopper to their real destination (`returnUrl`): their account when signed in, the public track
 * page when they checked out as a guest.
 *
 * UX rules this page follows:
 *  - Only a *successful* payment auto-forwards, and it does so on a visible countdown the shopper
 *    can pre-empt ("Continue now") — a result that vanishes in a blink reads as a glitch.
 *  - A cancellation and a decline are different events and get different colour, wording and next
 *    step. Neither ever auto-forwards: the shopper needs time to read what happened and choose.
 *  - The transaction reference is always on screen and copyable — it is the one string support will
 *    ask for if money moved but the order did not.
 */
@Component({
  selector: 'app-payment-madfoatcom-return',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Icon, Button],
  templateUrl: './payment-madfoatcom-return.html',
  styleUrl: './payment-madfoatcom-return.scss',
})
export class PaymentMadfoatcomReturn {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly payments = inject(PaymentsService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly state = signal<ReturnState>('verifying');
  protected readonly countdown = signal(AUTO_FORWARD_SECONDS);
  protected readonly copied = signal(false);

  private readonly params = this.route.snapshot.queryParamMap;
  protected readonly tranRef = this.params.get('tranRef') ?? '';
  protected readonly orderId = Number(this.params.get('orderId')) || 0;
  private readonly returnUrl = this.params.get('returnUrl') || '/account';

  /** The result heading takes focus once it resolves, so screen readers land on the outcome. */
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('heading');

  private timer: ReturnType<typeof setInterval> | null = null;

  protected readonly isVerifying = computed(() => this.state() === 'verifying');

  protected readonly icon = computed<IconName>(() => {
    switch (this.state()) {
      case 'paid':
        return 'check';
      case 'canceled':
        return 'return';
      default:
        return 'x';
    }
  });

  /**
   * Signed-in shoppers get sent to the order itself rather than the account root — that page carries
   * the "Pay again" button, so the recovery path is one tap instead of a hunt.
   */
  protected readonly recoveryUrl = computed(() =>
    this.orderId > 0 && this.returnUrl.startsWith('/account')
      ? `/account/orders/${this.orderId}`
      : this.returnUrl,
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopCountdown());

    effect(() => {
      if (this.isVerifying()) {
        return;
      }
      // Announce the outcome to assistive tech and put the keyboard where the next step is.
      this.heading()?.nativeElement.focus({ preventScroll: true });
    });

    if (!this.isBrowser) {
      return;
    }

    // No reference means the shopper never got as far as a transaction (or came back the long way
    // round). The order stays pending so they can retry from their account.
    if (!this.tranRef) {
      this.state.set('canceled');
      return;
    }

    this.payments.paytabsVerify({ tranRef: this.tranRef }).subscribe({
      next: (res) => {
        if (res.approved) {
          this.state.set('paid');
          this.startCountdown();
        } else {
          // Also covers PayTabs' pending states — the server left the order payable on purpose.
          this.state.set('failed');
        }
      },
      error: () => this.state.set('failed'),
    });
  }

  /** Forward to the shopper's destination. */
  protected leave(url = this.returnUrl): void {
    this.stopCountdown();
    if (url.startsWith('/')) {
      void this.router.navigateByUrl(url);
    } else {
      window.location.href = url;
    }
  }

  /** Puts the reference on the clipboard for a support ticket. No-op where the API is unavailable —
   * the reference is on screen either way. */
  protected copyRef(): void {
    void navigator.clipboard?.writeText(this.tranRef)?.then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }

  private startCountdown(): void {
    this.timer = setInterval(() => {
      const left = this.countdown() - 1;
      this.countdown.set(left);
      if (left <= 0) {
        this.leave();
      }
    }, 1000);
  }

  protected stopCountdown(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
