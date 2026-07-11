import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';
import { CatalogService } from 'data-access';
import { Icon } from 'ui';
import { CartStore } from '../core/cart.store';
import { CartDrawerService } from '../core/cart-drawer.service';
import { CategoryLabelPipe } from '../shared/category-label.pipe';
import { LanguageSwitcher } from './language-switcher';

interface MainLink {
  readonly key: string;
  readonly link: string;
  readonly queryParams?: Record<string, string>;
}

interface CategoryLink {
  readonly name: string;
  readonly category: string;
}

/**
 * Storefront chrome: announce bar, wordmark, a hardcoded primary nav
 * (Store Sections / Our News / About Us / New Arrivals), a secondary sub-nav of
 * the backend categories, and the search / account / cart icon actions (with a live
 * cart-count badge). On small screens both navs collapse into a logical-side
 * drawer. All copy is keyed through ngx-translate.
 */
@Component({
  selector: 'app-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    Icon,
    LanguageSwitcher,
    CategoryLabelPipe,
  ],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartStore);
  protected readonly cartDrawer = inject(CartDrawerService);
  private readonly catalog = inject(CatalogService);

  protected readonly menuOpen = signal(false);

  private readonly categories = this.catalog.categoriesResource();

  /**
   * Hardcoded primary nav. "Store Sections" opens the category hub (/categories)
   * so shoppers pick a section before seeing products; "New Arrivals" jumps to the
   * shop sorted newest-first.
   */
  protected readonly mainLinks: readonly MainLink[] = [
    { key: 'home', link: '/' },
    { key: 'sections', link: '/categories' },
    { key: 'news', link: '/news' },
    { key: 'about_us', link: '/pages/about-us' },
    { key: 'new_arrivals', link: '/shop', queryParams: { sort: 'newest' } },
  ];

  /**
   * Sub-nav, derived from the backend categories: top-level (no parent),
   * in-menu, with a usable name + slug, ordered by the admin's display order.
   */
  protected readonly categoryLinks = computed<readonly CategoryLink[]>(() =>
    (this.categories.value() ?? [])
      .filter((c) => c.includeInMenu && c.parentId === null && c.slug && c.name)
      .sort((a, b) => a.displayOrder - b.displayOrder)
      .map((c) => ({ name: c.name as string, category: c.slug as string })),
  );
}
