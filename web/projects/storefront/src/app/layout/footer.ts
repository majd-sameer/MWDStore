import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CatalogService } from 'data-access';
import { Icon } from 'ui';
import { CategoryLabelPipe } from '../shared/category-label.pipe';

interface FooterLink {
  readonly key: string;
  readonly link: string;
}

interface ShopLink {
  readonly name: string;
  readonly category: string;
}

/**
 * Site footer on the charcoal titanium surface: brand + mission + newsletter
 * capture and the Public Security Directorate endorsement crest, then three
 * link columns (Shop / MadeWithDetermination / Care). All copy keyed through
 * ngx-translate; layout uses logical Bootstrap utilities so it mirrors in RTL.
 */
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, CategoryLabelPipe],
  templateUrl: './footer.html',
  styleUrl: './footer.scss',
})
export class Footer {
  private readonly catalog = inject(CatalogService);

  protected readonly year = new Date().getFullYear();

  private readonly categories = this.catalog.categoriesResource();

  /** Top-level, in-menu categories from the backend (first four), mirroring the header nav. */
  protected readonly shopLinks = computed<readonly ShopLink[]>(() =>
    (this.categories.value() ?? [])
      .filter((c) => c.includeInMenu && c.parentId === null && c.slug && c.name)
      .sort((a, b) => a.displayOrder - b.displayOrder)
      .slice(0, 4)
      .map((c) => ({ name: c.name as string, category: c.slug as string })),
  );

  protected readonly brandLinks: readonly FooterLink[] = [
    { key: 'about', link: '/' },
    { key: 'makers', link: '/' },
    { key: 'stores', link: '/' },
  ];

  protected readonly careLinks: readonly FooterLink[] = [
    { key: 'delivery_returns', link: '/' },
    { key: 'track', link: '/account/orders' },
    { key: 'contact', link: '/' },
    { key: 'faq', link: '/' },
  ];
}
