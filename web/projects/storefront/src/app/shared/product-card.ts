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
 * Presentational product tile for the home rows and shop grid: gradient/image
 * art with an optional sale tag, name, rating, price (+ struck old price and
 * saving), and a round quick-add button. Owns no data — emits `addToCart` with
 * the product id. Copy is keyed through ngx-translate; layout is logical.
 */
@Component({
  selector: 'app-product-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Icon, Stars, Tag, Tile, CategoryLabelPipe],
  host: { class: 'product-card' },
  template: `
    <a class="pc-art" [routerLink]="['/products', product().id]" [attr.aria-label]="product().name">
      <lib-tile [src]="product().thumbnailImageUrl" [seed]="product().name ?? product().id"
        [alt]="product().name" ratio="1x1" />
      @if (!canOrder()) {
        <span class="pc-tag"><lib-tag tone="danger">{{ 'product.out_of_stock' | translate }}</lib-tag></span>
      } @else if (price().oldPrice) {
        <span class="pc-tag"><lib-tag tone="accent">{{ 'product.sale' | translate }}</lib-tag></span>
      }
    </a>

    <div class="pc-body">
      @if (product().categoryName; as categoryName) {
        <span class="pc-cat">{{ product().categorySlug | categoryLabel: categoryName }}</span>
      }
      <a class="pc-name-link" [routerLink]="['/products', product().id]">
        <h3 class="pc-name">{{ product().name }}</h3>
      </a>

      @if (product().ratingAverage; as rating) {
        <lib-stars [rating]="rating" [count]="product().reviewsCount" />
      } @else {
        <span class="pc-no-rating">
          <lib-stars [rating]="null" />
          {{ 'product.no_reviews' | translate }}
        </span>
      }

      <div class="pc-foot">
        <div class="pc-price">
          @if (product().isCallForPricing) {
            <span class="pc-call">{{ 'product.call_for_pricing' | translate }}</span>
          } @else {
            <span class="pc-now tabular-nums">{{ price().price | money }}</span>
            @if (price().oldPrice; as oldPrice) {
              <span class="pc-old tabular-nums">{{ oldPrice | money }}</span>
            }
          }
        </div>

        @if (!product().isCallForPricing) {
          <button
            type="button"
            class="pc-add"
            [class.is-disabled]="!canOrder()"
            [disabled]="!canOrder()"
            [attr.aria-label]="(canOrder() ? 'product.add' : 'product.out_of_stock') | translate"
            (click)="addToCart.emit(product())"
          >
            <lib-icon name="plus" [size]="18" />
          </button>
        }
      </div>
    </div>
  `,
  styles: `
    :host {
      padding: 10px;
      display: flex;
      flex-direction: column;
      block-size: 100%;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      transition: box-shadow 0.15s ease, transform 0.15s ease;
    }
    .pc-art {
      position: relative;
      display: block;
    }
    .pc-tag {
      position: absolute;
      inset-block-start: 0.7rem;
      inset-inline-start: 0.7rem;
    }
    .pc-body {
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
      padding-block: 0.85rem 0;
      flex: 1 1 auto;
    }
    .pc-name-link {
      text-decoration: none;
      color: var(--ink);
    }
    .pc-cat {
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--accent);
      letter-spacing: 0.02em;
    }
    .pc-name {
      font-size: 1rem;
      font-weight: 600;
      line-height: 1.3;
      margin: 0;
      display: -webkit-box;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      overflow: hidden;
    }
    .pc-name-link:hover .pc-name {
      color: var(--accent);
    }
    .pc-no-rating {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.8rem;
      color: var(--ink-3);
    }
    .pc-foot {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      margin-block-start: auto;
      padding-block-start: 0.35rem;
    }
    .pc-price {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
    }
    .pc-now {
      font-weight: 700;
      font-size: 1.05rem;
      color: var(--ink);
    }
    .pc-old {
      font-size: 0.85rem;
      color: var(--ink-3);
      text-decoration: line-through;
    }
    .pc-call {
      font-size: 0.85rem;
      color: var(--ink-2);
    }
    .pc-add {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
      inline-size: 38px;
      block-size: 38px;
      border: 0;
      border-radius: 50%;
      background: var(--green);
      color: #fff;
      cursor: pointer;
      transition: background 0.15s ease;
    }
    .pc-add:hover {
      background: var(--green-strong);
    }
    .pc-add.is-disabled {
      background: var(--surface-3);
      color: var(--ink-3);
      cursor: not-allowed;
    }
  `,
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
