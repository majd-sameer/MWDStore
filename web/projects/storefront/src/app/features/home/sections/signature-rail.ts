import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { ProductListItem } from 'data-access';
import { Icon } from 'ui';
import { ProductCardSignature } from '../../../shared/product-card-signature';

/**
 * Home rail for curated "Signature" products, placed above Best Sellers. Mirrors
 * {@link FeaturedRow}'s layout but renders the distinct {@link ProductCardSignature}. Renders
 * nothing when there are no signature products.
 */
@Component({
  selector: 'app-signature-rail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, ProductCardSignature],
  templateUrl: './signature-rail.html',
  styleUrl: './signature-rail.scss',
})
export class SignatureRail {
  readonly products = input.required<readonly ProductListItem[]>();
  readonly addToCart = output<ProductListItem>();
}
