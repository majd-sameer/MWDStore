import { MoneyPipe } from 'core';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { CartItemModel } from 'data-access';
import { Button, Icon, Stepper, Tile, ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';

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

  protected total(): number {
    const cart = this.store.cart();
    return cart ? cart.subTotal - cart.discount : 0;
  }

  protected changeQty(item: CartItemModel, quantity: number): void {
    if (quantity < 1 || quantity === item.quantity) {
      return;
    }
    this.busy.set(true);
    this.store.update(item.id, { quantity }).subscribe({
      next: () => this.busy.set(false),
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('common.error'));
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
