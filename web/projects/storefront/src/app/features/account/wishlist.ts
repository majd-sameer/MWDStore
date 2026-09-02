import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  StorefrontFeaturesService,
  type WishListItemDto,
} from 'data-access';
import { Button, Tile, ToastService } from 'ui';
import { CartStore, type CartProduct } from '../../core/cart.store';
import { announceCartAdd, announceCartError } from '../../core/cart-messages';

/** The customer's wishlist: saved products with quick add-to-cart and remove. */
@Component({
  selector: 'app-wishlist',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, TranslatePipe, RouterLink, Button, Tile],
  templateUrl: './wishlist.html',
  styleUrl: './wishlist.scss',
})
export class Wishlist {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly cart = inject(CartStore);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

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
      next: (cart) => announceCartAdd(this.toast, this.translate, cart),
      error: (error) => announceCartError(this.toast, this.translate, error),
    });
  }

  protected remove(item: WishListItemDto): void {
    this.service.removeFromWishlist(item.id).subscribe({
      next: () => this.items.update((items) => items.filter((i) => i.id !== item.id)),
      error: () => this.toast.error(this.translate.instant('wishlist.error')),
    });
  }
}
