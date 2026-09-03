import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { CartItemModel } from 'data-access';
import { Button, Icon, Stepper, Tile, ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';
import { CartDrawerService } from '../../core/cart-drawer.service';
import { announceCartError, cartAdjustmentMessage } from '../../core/cart-messages';

/**
 * Slide-in bag drawer rendered in the app shell. Opened from the header, and
 * automatically after every add-to-bag as the confirmation step: it shows what
 * went in, lets the shopper fix the quantity or remove a line without leaving
 * the page, and offers checkout / view bag / keep shopping. Anchored to the
 * inline-end side so it mirrors correctly in RTL. Closes on Escape, backdrop
 * click or the close button; focus moves into the panel when it opens.
 */
@Component({
  selector: 'app-cart-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Button, Icon, Tile, Stepper],
  templateUrl: './cart-drawer.html',
  styleUrl: './cart-drawer.scss',
})
export class CartDrawer {
  protected readonly store = inject(CartStore);
  protected readonly drawer = inject(CartDrawerService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly busy = signal(false);

  constructor() {
    // Move keyboard focus into the dialog when it opens so Escape / Tab work
    // from the drawer, not from the button that opened it.
    afterRenderEffect(() => {
      const panel = this.panel();
      if (this.drawer.open() && panel) {
        panel.nativeElement.focus({ preventScroll: true });
      }
    });
  }

  protected maxQty(item: CartItemModel): number {
    return item.productStockTrackingIsEnabled ? Math.max(1, item.availableQuantity) : 99;
  }

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
      next: () => this.busy.set(false),
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }
}
