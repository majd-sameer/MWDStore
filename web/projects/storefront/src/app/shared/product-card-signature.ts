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

/**
 * Distinct "Signature" variant of {@link ProductCard} — same input contract, so `/shop` can swap the
 * component by `product.isSignature`. Adds a gold frame + corner badge using existing design tokens
 * (no forked inline styles); the wash adapts to dark mode via color-mix over `--surface`.
 */
@Component({
  selector: 'app-product-card-signature',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Icon, Stars, Tag, Tile, CategoryLabelPipe],
  host: { class: 'product-card-signature' },
  templateUrl: './product-card-signature.html',
  styleUrl: './product-card-signature.scss',
})
export class ProductCardSignature {
  readonly product = input.required<ProductListItem>();
  readonly addToCart = output<ProductListItem>();

  protected readonly price = computed(() => this.product().calculatedProductPrice);
  protected readonly canOrder = computed(() => {
    const product = this.product();
    return product.isAllowToOrder && (product.stockQuantity ?? 1) !== 0;
  });
}
