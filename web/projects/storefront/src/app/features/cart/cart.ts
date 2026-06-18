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
  template: `
    <h1 class="page-title">{{ 'cart.title' | translate }}</h1>

    @if (store.isLoading()) {
      <div class="state">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (store.items().length === 0) {
      <div class="state empty">
        <p class="lead">{{ 'cart.empty' | translate }}</p>
        <p class="text-body-secondary">{{ 'cart.empty_sub' | translate }}</p>
        <a libButton variant="dark" size="lg" routerLink="/shop">{{ 'cart.browse' | translate }}</a>
      </div>
    } @else {
      <div class="cart">
        <div class="lines">
          @for (item of store.items(); track item.id) {
            <div class="line">
              <a class="line-art" [routerLink]="['/products', item.productId]">
                <lib-tile [src]="item.productImageUrl" [seed]="item.productName ?? item.productId"
                  [alt]="item.productName" ratio="1x1" />
              </a>

              <div class="line-info">
                <a class="line-name" [routerLink]="['/products', item.productId]">{{ item.productName }}</a>
                @if (!item.isProductAvailableToOrder) {
                  <div class="line-warn">{{ 'cart.unavailable' | translate }}</div>
                }
                <div class="line-unit tabular-nums">{{ item.calculatedProductPrice.price | money }}</div>
              </div>

              <lib-stepper
                [value]="item.quantity"
                [min]="1"
                [max]="item.productStockQuantity || 99"
                [disabled]="busy()"
                (valueChange)="changeQty(item, $event)"
              />

              <div class="line-total tabular-nums">
                {{ item.calculatedProductPrice.price * item.quantity | money }}
              </div>

              <button
                type="button"
                class="line-remove"
                [disabled]="busy()"
                [attr.aria-label]="'cart.remove' | translate"
                (click)="remove(item)"
              >
                <lib-icon name="trash" [size]="18" />
              </button>
            </div>
          }
        </div>

        <aside class="summary">
          <h2 class="summary-title">{{ 'checkout.summary' | translate }}</h2>
          <dl class="summary-rows">
            <dt>{{ 'cart.subtotal' | translate }}</dt>
            <dd class="tabular-nums">{{ store.subTotal() | money }}</dd>
            @if (store.cart()?.discount) {
              <dt>{{ 'cart.discount' | translate }}</dt>
              <dd class="text-success tabular-nums">−{{ store.cart()?.discount | money }}</dd>
            }
            <dt class="ship">{{ 'cart.shipping' | translate }}</dt>
            <dd class="ship text-body-secondary">{{ 'cart.shipping_note' | translate }}</dd>
          </dl>
          <div class="summary-total">
            <span>{{ 'cart.total' | translate }}</span>
            <strong class="tabular-nums">{{ total() | money }}</strong>
          </div>
          <a libButton variant="dark" size="lg" [block]="true" class="mt-3" routerLink="/checkout">
            {{ 'cart.checkout' | translate }}
          </a>
          <a class="continue" routerLink="/shop">{{ 'cart.continue' | translate }}</a>
        </aside>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .page-title {
      font-weight: 700;
      font-size: clamp(2rem, 4vw, 2.75rem);
      letter-spacing: -0.02em;
      margin-block: 1rem 2rem;
    }
    .state {
      text-align: center;
      padding-block: 4rem;
    }
    .empty .lead {
      font-weight: 600;
      margin-block-end: 0.25rem;
    }
    .cart {
      display: grid;
      grid-template-columns: 1fr 340px;
      gap: 3rem;
      align-items: start;
    }
    @media (max-width: 900px) {
      .cart {
        grid-template-columns: 1fr;
        gap: 2rem;
      }
    }
    .line {
      display: grid;
      grid-template-columns: 84px 1fr auto auto auto;
      gap: 1rem;
      align-items: center;
      padding-block: 1.25rem;
      border-block-end: 1px solid var(--line);
    }
    @media (max-width: 560px) {
      .line {
        grid-template-columns: 64px 1fr auto;
        grid-template-areas: 'art info remove' 'art qty total';
      }
      .line-art { grid-area: art; }
      .line-info { grid-area: info; }
    }
    .line-art {
      display: block;
      inline-size: 84px;
    }
    .line-name {
      font-weight: 600;
      color: var(--ink);
      text-decoration: none;
    }
    .line-name:hover {
      color: var(--accent);
    }
    .line-warn {
      font-size: 0.82rem;
      color: var(--danger, #d6455d);
    }
    .line-unit {
      font-size: 0.88rem;
      color: var(--ink-3);
      margin-block-start: 0.2rem;
    }
    .line-total {
      font-weight: 700;
      min-inline-size: 5rem;
      text-align: end;
    }
    .line-remove {
      border: 0;
      background: transparent;
      color: var(--ink-3);
      cursor: pointer;
    }
    .line-remove:hover {
      color: var(--danger, #d6455d);
    }
    .summary {
      position: sticky;
      inset-block-start: 88px;
      background: var(--surface-2);
      border-radius: var(--r-lg);
      padding: 1.5rem;
    }
    .summary-title {
      font-size: 1.1rem;
      font-weight: 700;
      margin-block-end: 1rem;
    }
    .summary-rows {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 0.5rem 1rem;
      margin: 0;
    }
    .summary-rows dt {
      font-weight: 400;
      color: var(--ink-2);
    }
    .summary-rows dd {
      margin: 0;
      text-align: end;
    }
    .summary-rows .ship {
      font-size: 0.85rem;
    }
    .summary-total {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin-block-start: 1rem;
      padding-block-start: 1rem;
      border-block-start: 1px solid var(--line);
      font-size: 1.15rem;
      font-weight: 700;
    }
    .continue {
      display: block;
      text-align: center;
      margin-block-start: 0.85rem;
      color: var(--ink-2);
      text-decoration: none;
      font-weight: 600;
    }
    .continue:hover {
      color: var(--accent);
    }
  `,
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
