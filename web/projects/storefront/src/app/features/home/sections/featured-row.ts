import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { ProductListItem } from 'data-access';
import { Icon } from 'ui';
import { ProductCard } from '../../../shared/product-card';

/**
 * Reusable featured product row: eyebrow + title + a "view all" link over a
 * four-up product grid. Presentational — products are passed in from the page;
 * `addToCart` bubbles the chosen product id up. Renders nothing when empty.
 */
@Component({
  selector: 'app-featured-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, ProductCard],
  template: `
    @if (products().length) {
      <section class="featured">
        <div class="featured-head">
          <div>
            <div class="eyebrow">{{ eyebrow() | translate }}</div>
            <h2 class="featured-title">{{ title() | translate }}</h2>
          </div>
          <a class="featured-all" [routerLink]="viewAllLink()" [queryParams]="viewAllParams()">
            {{ 'home.view_all' | translate }}
            <lib-icon name="arrowEnd" [size]="16" />
          </a>
        </div>

        <div class="row row-cols-2 row-cols-md-4 g-4">
          @for (product of products(); track product.id) {
            <div class="col">
              <app-product-card [product]="product" (addToCart)="addToCart.emit($event)" />
            </div>
          }
        </div>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
      padding-block: 3rem;
    }
    .featured-head {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      margin-block-end: 1.75rem;
    }
    .eyebrow {
      font-size: 0.78rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.1em;
      color: var(--accent);
      margin-block-end: 0.4rem;
    }
    .featured-title {
      font-weight: 700;
      font-size: clamp(1.6rem, 3vw, 2.2rem);
      letter-spacing: -0.02em;
      margin: 0;
    }
    .featured-all {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      color: var(--navy);
      font-weight: 700;
      text-decoration: none;
      white-space: nowrap;
    }
    .featured-all:hover {
      color: var(--accent);
    }
  `,
})
export class FeaturedRow {
  readonly eyebrow = input.required<string>();
  readonly title = input.required<string>();
  readonly products = input.required<readonly ProductListItem[]>();
  readonly viewAllLink = input<string | readonly unknown[]>('/shop');
  readonly viewAllParams = input<Record<string, string>>({});

  readonly addToCart = output<ProductListItem>();
}
