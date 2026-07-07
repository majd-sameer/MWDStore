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
  templateUrl: './featured-row.html',
  styleUrl: './featured-row.scss',
})
export class FeaturedRow {
  readonly eyebrow = input.required<string>();
  readonly title = input.required<string>();
  readonly products = input.required<readonly ProductListItem[]>();
  readonly viewAllLink = input<string | readonly unknown[]>('/shop');
  readonly viewAllParams = input<Record<string, string>>({});

  readonly addToCart = output<ProductListItem>();
}
