import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { CartItemModel } from 'data-access';
import { Button, Icon, Stepper, Tile, ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';
import { announceCartError, cartAdjustmentMessage } from '../../core/cart-messages';

/**
 * Full bag page: line items with thumbnail, quantity stepper and remove, plus a
 * sticky order-summary aside. Reuses the shared CartStore commands; copy keyed;
 * layout logical.
 */
@Component({
  selector: 'app-cart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Button, Icon, Stepper, Tile],
  templateUrl: './cart.html',
  styleUrl: './cart.scss',
})
export class Cart {
  protected readonly store = inject(CartStore);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly busy = signal(false);

  /**
   * Lines that can no longer be bought — a withdrawn product, or fewer left than the line asks for.
   * They are typically here because a failed order was returned to the cart. The server leaves them
   * out of the totals, and checkout stays closed until they are removed (it would fail on them).
   */
  protected readonly unavailable = computed(() =>
    this.store.items().filter((item) => !item.isAvailable),
  );

  protected total(): number {
    const cart = this.store.cart();
    return cart ? cart.subTotal - cart.discount : 0;
  }

  /**
   * The most a line may be set to: what is actually available for a stock-tracked product, otherwise
   * a generous cap. `availableQuantity` is the product's stock net of every order holding units, so a
   * line whose stock has since been taken by someone else's order shows the smaller, honest ceiling.
   */
  protected maxQty(item: CartItemModel): number {
    return item.productStockTrackingIsEnabled ? Math.max(1, item.availableQuantity) : 99;
  }

  /**
   * Whether the stepper can still do anything useful for this line. An over-stock line is exactly the
   * one the shopper needs to turn *down*, so it stays live — but a withdrawn product, or one with
   * nothing left at all, can only be removed, and a stepper whose max sits below the current value
   * would move "+" the wrong way.
   */
  protected canAdjust(item: CartItemModel): boolean {
    return (
      item.isProductAvailableToOrder &&
      (!item.productStockTrackingIsEnabled || item.availableQuantity > 0)
    );
  }

  protected changeQty(item: CartItemModel, quantity: number): void {
    if (quantity < 1 || quantity === item.quantity) {
      return;
    }
    this.busy.set(true);
    this.store.update(item.id, { quantity }).subscribe({
      next: (cart) => {
        this.busy.set(false);
        const capped = cartAdjustmentMessage(cart);
        if (capped) {
          this.toast.success(this.translate.instant(capped.key, capped.params));
        }
      },
      error: (error) => {
        this.busy.set(false);
        announceCartError(this.toast, this.translate, error);
      },
    });
  }

  protected remove(item: CartItemModel): void {
    this.busy.set(true);
    this.store.remove(item.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success(this.translate.instant('cart.remove'));
      },
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }
}
