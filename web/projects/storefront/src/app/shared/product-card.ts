import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { ProductListItem } from 'data-access';
import { Icon, Stars, Tag, Tile } from 'ui';
import { CategoryLabelPipe } from './category-label.pipe';
import { BagQty } from './bag-qty';

/**
 * Presentational product tile for the home rows and shop grid: gradient/image
 * art with an optional sale tag, name, rating, price (+ struck old price and
 * saving), and a round quick-add button. Owns no data — emits `addToCart` with
 * the product id. Copy is keyed through ngx-translate; layout is logical.
 */
@Component({
  selector: 'app-product-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Icon, Stars, Tag, Tile, CategoryLabelPipe, BagQty],
  host: { class: 'product-card' },
  templateUrl: './product-card.html',
  styleUrl: './product-card.scss',
})
export class ProductCard {
  readonly product = input.required<ProductListItem>();
  readonly addToCart = output<ProductListItem>();

  protected readonly price = computed(() => this.product().calculatedProductPrice);
  protected readonly canOrder = computed(() => {
    const product = this.product();
    return product.isAllowToOrder && (product.stockQuantity ?? 1) !== 0;
  });
}
