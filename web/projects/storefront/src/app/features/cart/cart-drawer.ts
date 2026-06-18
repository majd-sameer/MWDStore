import { MoneyPipe } from 'core';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Button, Icon, Tile } from 'ui';
import { CartStore } from '../../core/cart.store';
import { CartDrawerService } from '../../core/cart-drawer.service';

/**
 * Slide-in bag drawer rendered in the app shell and opened from the header.
 * Anchored to the inline-end side so it mirrors correctly in RTL. Reads the
 * shared CartStore; copy is keyed.
 */
@Component({
  selector: 'app-cart-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Button, Icon, Tile],
  template: `
    @if (drawer.open()) {
      <button
        type="button"
        class="backdrop"
        [attr.aria-label]="'cart.title' | translate"
        (click)="drawer.close()"
      ></button>
      <aside class="panel" role="dialog" [attr.aria-label]="'cart.title' | translate">
        <header class="panel-head">
          <h2 class="panel-title">{{ 'cart.title' | translate }}</h2>
          <button type="button" class="icon-btn" aria-label="Close" (click)="drawer.close()">
            <lib-icon name="x" [size]="22" />
          </button>
        </header>

        @if (store.items().length === 0) {
          <div class="empty">
            <p class="empty-title">{{ 'cart.empty' | translate }}</p>
            <a libButton variant="dark" routerLink="/shop" (click)="drawer.close()">
              {{ 'cart.browse' | translate }}
            </a>
          </div>
        } @else {
          <div class="items">
            @for (item of store.items(); track item.id) {
              <div class="item">
                <a
                  class="item-art"
                  [routerLink]="['/products', item.productId]"
                  (click)="drawer.close()"
                >
                  <lib-tile [src]="item.productImageUrl" [seed]="item.productName ?? item.productId"
                    [alt]="item.productName" ratio="1x1" />
                </a>
                <div class="item-body">
                  <a
                    class="item-name"
                    [routerLink]="['/products', item.productId]"
                    (click)="drawer.close()"
                    >{{ item.productName }}</a
                  >
                  <div class="item-meta">
                    <span class="tabular-nums">{{ item.quantity }} ×</span>
                    <span class="tabular-nums">{{ item.calculatedProductPrice.price | money }}</span>
                  </div>
                </div>
                <div class="item-total tabular-nums">
                  {{ item.calculatedProductPrice.price * item.quantity | money }}
                </div>
              </div>
            }
          </div>

          <footer class="panel-foot">
            <div class="subtotal">
              <span>{{ 'cart.subtotal' | translate }}</span>
              <strong class="tabular-nums">{{ store.subTotal() | money }}</strong>
            </div>
            <p class="ship-note">{{ 'cart.shipping_note' | translate }}</p>
            <a
              libButton
              variant="dark"
              size="lg"
              [block]="true"
              routerLink="/checkout"
              (click)="drawer.close()"
            >
              {{ 'cart.checkout' | translate }}
            </a>
            <a
              libButton
              variant="secondary"
              [outline]="true"
              [block]="true"
              class="mt-2"
              routerLink="/cart"
              (click)="drawer.close()"
            >
              {{ 'cart.view_bag' | translate }}
            </a>
          </footer>
        }
      </aside>
    }
  `,
  styles: `
    .backdrop {
      position: fixed;
      inset: 0;
      border: 0;
      padding: 0;
      background: rgba(26, 23, 48, 0.4);
      cursor: pointer;
      z-index: 1050;
    }
    .panel {
      position: fixed;
      inset-block: 0;
      inset-inline-end: 0;
      inline-size: min(92vw, 420px);
      z-index: 1055;
      display: flex;
      flex-direction: column;
      background: var(--surface);
      border-inline-start: 1px solid var(--line);
      box-shadow: var(--shadow-lg);
    }
    .panel-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1.25rem 1.5rem;
      border-block-end: 1px solid var(--line);
    }
    .panel-title {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 700;
    }
    .icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 38px;
      block-size: 38px;
      border: 0;
      border-radius: 50%;
      background: transparent;
      color: var(--ink);
      cursor: pointer;
    }
    .icon-btn:hover {
      background: var(--surface-2);
    }
    .empty {
      flex: 1 1 auto;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      padding: 2rem;
    }
    .empty-title {
      color: var(--ink-2);
      margin: 0;
    }
    .items {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 1rem 1.5rem;
    }
    .item {
      display: grid;
      grid-template-columns: 56px 1fr auto;
      gap: 0.85rem;
      align-items: center;
      padding-block: 0.85rem;
      border-block-end: 1px solid var(--line-2);
    }
    .item-art {
      display: block;
      inline-size: 56px;
    }
    .item-name {
      font-weight: 600;
      color: var(--ink);
      text-decoration: none;
    }
    .item-name:hover {
      color: var(--accent);
    }
    .item-meta {
      display: flex;
      gap: 0.4rem;
      font-size: 0.85rem;
      color: var(--ink-3);
      margin-block-start: 0.2rem;
    }
    .item-total {
      font-weight: 600;
    }
    .panel-foot {
      padding: 1.25rem 1.5rem;
      border-block-start: 1px solid var(--line);
    }
    .subtotal {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      font-size: 1.05rem;
      margin-block-end: 0.25rem;
    }
    .ship-note {
      font-size: 0.82rem;
      color: var(--ink-3);
      margin-block-end: 1rem;
    }
  `,
})
export class CartDrawer {
  protected readonly store = inject(CartStore);
  protected readonly drawer = inject(CartDrawerService);

  protected readonly count = computed(() => this.store.count());
}
