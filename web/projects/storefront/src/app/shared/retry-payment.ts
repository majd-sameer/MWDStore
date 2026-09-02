import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { OrderService, PaymentsService } from 'data-access';
import { Button, ToastService } from 'ui';
import { CartStore } from '../core/cart.store';

/** Order statuses a shopper can still pay from: New, PendingPayment, PaymentFailed. */
const PAYABLE_STATUSES: readonly number[] = [1, 20, 35];

/**
 * "Pay again" for an order whose payment failed (a declined card, a wrong CVV, a shopper who backed
 * out of the gateway page).
 *
 * The server decides what "again" means. If every line is still orderable it clears a fresh payment
 * for the same order and we send the shopper straight back to the gateway. If anything is gone, it
 * returns the whole order to the cart and cancels it — we then land the shopper on the cart, where
 * what they can still buy is priced as usual and the rest is listed as unavailable.
 */
@Component({
  selector: 'app-retry-payment',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Button],
  templateUrl: './retry-payment.html',
})
export class RetryPayment {
  private readonly orders = inject(OrderService);
  private readonly payments = inject(PaymentsService);
  private readonly cart = inject(CartStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly orderId = input.required<number>();
  readonly orderStatus = input.required<number>();
  readonly paymentMethod = input<string | null>(null);
  readonly orderTotal = input<number>(0);

  protected readonly busy = signal(false);

  /** Only an unpaid, uncanceled order placed with an online gateway can be paid again. */
  protected readonly canRetry = computed(() => {
    const method = this.paymentMethod();
    return PAYABLE_STATUSES.includes(this.orderStatus()) && !!method && method !== 'CoD';
  });

  protected retry(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);

    this.orders.retryPayment(this.orderId()).subscribe({
      next: (result) => {
        if (result.movedToCart) {
          this.goToCart(result.unavailableItems.length);
          return;
        }
        this.startPayment();
      },
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }

  /** Something is no longer buyable: the order is now in the cart, minus nothing — flagged, not dropped. */
  private goToCart(missing: number): void {
    this.busy.set(false);
    this.cart.reload();
    this.toast.error(
      missing
        ? this.translate.instant('account.retry_moved_to_cart', { count: missing })
        : this.translate.instant('account.retry_moved_to_cart_all'),
    );
    void this.router.navigateByUrl('/cart');
  }

  private startPayment(): void {
    const method = this.paymentMethod() ?? '';
    const returnUrl = '/account';

    this.payments.initiate({ orderId: this.orderId(), method, returnUrl }).subscribe({
      next: (res) => {
        this.busy.set(false);
        if (res.isSandbox) {
          void this.router.navigate(['/payment/mock'], {
            queryParams: {
              orderId: res.orderId,
              paymentId: res.paymentId,
              method: res.method,
              amount: this.orderTotal(),
              returnUrl,
            },
          });
        } else if (this.isBrowser) {
          window.location.href = res.redirectUrl;
        }
      },
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('checkout.payment_start_error'));
      },
    });
  }
}
