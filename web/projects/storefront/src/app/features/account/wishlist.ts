import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  StorefrontFeaturesService,
  type WishListItemDto,
} from 'data-access';
import { Button, Tile, ToastService } from 'ui';
import { CartStore, type CartProduct } from '../../core/cart.store';

/** The customer's wishlist: saved products with quick add-to-cart and remove. */
@Component({
  selector: 'app-wishlist',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, TranslatePipe, RouterLink, Button, Tile],
  template: `
    <div class="container py-4">
      <h1 class="page-title">{{ 'wishlist.title' | translate }}</h1>

      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      } @else if (!items().length) {
        <div class="text-center py-5">
          <p class="text-body-secondary">{{ 'wishlist.empty' | translate }}</p>
          <a libButton variant="dark" routerLink="/shop">{{ 'cart.browse' | translate }}</a>
        </div>
      } @else {
        <div class="wl-grid">
          @for (item of items(); track item.id) {
            <div class="wl-card">
              <a [routerLink]="['/products', item.productId]">
                <lib-tile [src]="item.thumbnailUrl" [seed]="item.productName ?? item.productId"
                  [alt]="item.productName" ratio="1x1" />
              </a>
              <div class="wl-body">
                <a class="wl-name" [routerLink]="['/products', item.productId]">{{ item.productName }}</a>
                <div class="wl-price tabular-nums">{{ item.price | money }}</div>
                @if (!item.isAvailable) {
                  <div class="small text-warning">{{ 'wishlist.unavailable' | translate }}</div>
                }
                <div class="d-flex gap-2 mt-2">
                  @if (item.isAvailable) {
                    <button type="button" libButton variant="dark" size="sm"
                      (click)="addToCart(item)">
                      {{ 'product.add' | translate }}
                    </button>
                  }
                  <button type="button" libButton variant="secondary" size="sm" [outline]="true"
                    (click)="remove(item)">
                    {{ 'wishlist.remove' | translate }}
                  </button>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .page-title {
      font-size: 1.6rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      margin-block-end: 1.25rem;
    }
    .wl-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));
      gap: 1rem;
    }
    .wl-card {
      border: 1px solid var(--surface-3, #eee);
      border-radius: 0.75rem;
      padding: 0.75rem;
    }
    .wl-name {
      display: block;
      font-weight: 600;
      color: var(--ink, inherit);
      text-decoration: none;
      margin-block-start: 0.5rem;
    }
    .wl-price {
      font-weight: 700;
    }
  `,
})
export class Wishlist {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly cart = inject(CartStore);
  private readonly toast = inject(ToastService);

  protected readonly items = signal<WishListItemDto[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.load();
  }

  private load(): void {
    this.service.wishlist().subscribe({
      next: (wishlist) => {
        this.items.set(wishlist.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected addToCart(item: WishListItemDto): void {
    const product: CartProduct = {
      id: item.productId,
      name: item.productName,
      thumbnailImageUrl: item.thumbnailUrl,
      calculatedProductPrice: { price: item.price, oldPrice: null, percentOfSaving: 0 },
      stockQuantity: null,
      isAllowToOrder: item.isAvailable,
    };
    this.cart.add(product, item.quantity || 1).subscribe({
      next: () => this.toast.success('product.added'),
      error: () => this.toast.error('common.error'),
    });
  }

  protected remove(item: WishListItemDto): void {
    this.service.removeFromWishlist(item.id).subscribe({
      next: () => this.items.update((items) => items.filter((i) => i.id !== item.id)),
      error: () => this.toast.error('wishlist.error'),
    });
  }
}
