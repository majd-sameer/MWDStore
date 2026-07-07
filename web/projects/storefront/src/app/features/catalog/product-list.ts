import { isPlatformBrowser, NgTemplateOutlet } from '@angular/common';
import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  CatalogService,
  type CatalogProductQuery,
  type ProductListItem,
} from 'data-access';
import {
  Breadcrumb,
  type BreadcrumbItem,
  Icon,
  Pagination,
  Stars,
  Tile,
  ToastService,
} from 'ui';
import { CartStore } from '../../core/cart.store';
import { SeoService } from '../../core/seo.service';
import { ProductCard } from '../../shared/product-card';
import { CategoryLabelPipe } from '../../shared/category-label.pipe';

const PAGE_SIZE = 12;

function toNumber(value: string | null): number | undefined {
  if (value === null || value === '') {
    return undefined;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

/**
 * Store / listing page (STORE-PAGE.md): an ivory page-header band, then a
 * 2-column layout — sticky filter sidebar (category radios with counts, max
 * price slider, producing-center checkboxes, rating radios, reset) and the
 * product area with a toolbar (result count, grid/list view toggle, sort) over
 * a card grid or horizontal rows. Below 980px the sidebar folds into a slide-in
 * sheet. Filter state lives entirely in the URL query params (shareable and
 * SSR-safe); the API does the filtering and returns the facets the sidebar
 * renders from. Copy is keyed; layout uses logical properties so RTL mirrors.
 */
@Component({
  selector: 'app-product-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MoneyPipe,
    NgTemplateOutlet,
    RouterLink,
    TranslatePipe,
    ProductCard,
    Pagination,
    Breadcrumb,
    Icon,
    Stars,
    Tile,
    CategoryLabelPipe,
  ],
  templateUrl: './product-list.html',
  styleUrl: './product-list.scss',
})
export class ProductList {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cart = inject(CartStore);
  private readonly seo = inject(SeoService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly ratingOptions = [4, 4.5];
  protected readonly view = signal<'grid' | 'list'>('grid');
  protected readonly sheetOpen = signal(false);

  // The grid/list toggle is hidden ≤542px (always card view on mobile). Force the
  // grid renderer there too, so resizing down from a list selection still shows cards.
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly isNarrow = signal(false);
  protected readonly effectiveView = computed<'grid' | 'list'>(() =>
    this.isNarrow() ? 'grid' : this.view(),
  );

  private readonly params = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  protected readonly query = computed<CatalogProductQuery>(() => {
    const params = this.params();
    return {
      query: params.get('q') ?? undefined,
      category: params.get('category') ?? undefined,
      brand: params.get('brand') ?? undefined,
      sort: params.get('sort') ?? undefined,
      minPrice: toNumber(params.get('minPrice')),
      maxPrice: toNumber(params.get('maxPrice')),
      minRating: toNumber(params.get('minRating')),
      page: toNumber(params.get('page')) ?? 1,
      pageSize: PAGE_SIZE,
    };
  });

  protected readonly result = this.catalog.productsResource(this.query);

  /** Last loaded result — keeps the sidebar facets and product area rendered
   *  (dimmed) while a filter change refetches, instead of flashing a spinner. */
  private readonly lastValue = signal<ReturnType<typeof this.result.value>>(undefined);
  protected readonly data = computed(() => this.result.value() ?? this.lastValue());

  protected readonly totalProduct = computed(() => this.data()?.totalProduct ?? 0);

  /** Current page's products reordered so available items come first; the sort
   *  is stable, so the API order is preserved within the in-stock / sold-out
   *  groups. Call-for-pricing items count as available (they stay up top). */
  protected readonly products = computed<ProductListItem[]>(() => {
    const items = this.data()?.products ?? [];
    return [...items].sort(
      (a, b) => Number(this.isOutOfStock(a)) - Number(this.isOutOfStock(b)),
    );
  });

  protected readonly categories = computed(() => {
    const cats = this.data()?.filterOption?.categories ?? [];
    return [...cats].sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));
  });

  /** Every seeded product belongs to exactly one category, so the facet sum is
   *  the catalog total for the "all products" row. */
  protected readonly allCount = computed(() =>
    this.categories().reduce((sum, c) => sum + c.count, 0),
  );

  protected readonly brands = computed(() => this.data()?.filterOption?.brands ?? []);

  protected readonly selectedBrands = computed(
    () => this.query().brand?.split('--').filter(Boolean) ?? [],
  );

  protected readonly priceFacet = computed(() => {
    const price = this.data()?.filterOption?.price;
    return price && price.maxPrice > price.minPrice ? price : null;
  });
  protected readonly sliderMin = computed(() =>
    Math.floor(this.priceFacet()?.minPrice ?? 0),
  );
  protected readonly sliderMax = computed(() =>
    Math.ceil(this.priceFacet()?.maxPrice ?? 0),
  );
  protected readonly sliderValue = computed(
    () => this.query().maxPrice ?? this.sliderMax(),
  );

  /**
   * Translated name of the active category for the pagehead H1 / breadcrumb.
   * Resolved from the filter options, then translated by slug
   * (`categories.<slug>`) with a fallback to the backend name — mirroring the
   * CategoryLabelPipe used elsewhere.
   */
  private readonly activeCategoryName = computed(() => {
    const slug = this.query().category;
    if (!slug) {
      return null;
    }
    const match = this.data()?.filterOption?.categories?.find((c) => c.slug === slug);
    if (!match) {
      return null;
    }
    const key = `categories.${slug}`;
    const label = this.translate.instant(key);
    return label === key ? (match.name ?? null) : label;
  });

  protected readonly title = computed(() => {
    const q = this.query();
    if (q.query) {
      return this.translate.instant('shop.search_title', { query: q.query });
    }
    return this.activeCategoryName() ?? this.translate.instant('shop.all_products');
  });

  protected readonly crumbs = computed<BreadcrumbItem[]>(() => {
    const items: BreadcrumbItem[] = [
      { label: this.translate.instant('common.home'), link: '/' },
    ];
    const category = this.activeCategoryName();
    if (category) {
      items.push({ label: this.translate.instant('shop.title'), link: '/shop' });
      items.push({ label: category });
    } else {
      items.push({ label: this.translate.instant('shop.title') });
    }
    return items;
  });

  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return Boolean(
      q.query || q.category || q.brand || q.minPrice || q.maxPrice || q.minRating || q.sort,
    );
  });

  constructor() {
    if (this.isBrowser) {
      const mq = window.matchMedia('(max-width: 542px)');
      this.isNarrow.set(mq.matches);
      mq.addEventListener('change', (event) => this.isNarrow.set(event.matches));
    }

    effect(() => {
      const value = this.result.value();
      if (value) {
        this.lastValue.set(value);
      }
    });

    effect(() => {
      const total = this.data()?.totalProduct;
      this.seo.update({
        title: this.title(),
        description:
          total !== undefined
            ? `Browse ${total} handmade products at MadeWithDetermination.`
            : 'Browse handmade products at MadeWithDetermination.',
      });
    });
  }

  protected setCategory(slug: string | null): void {
    this.navigate({ category: slug });
  }

  protected setSort(event: Event): void {
    this.navigate({ sort: (event.target as HTMLSelectElement).value || null });
  }

  protected applyMaxPrice(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.navigate({ maxPrice: value >= this.sliderMax() ? null : value });
  }

  protected setRating(value: number | null): void {
    this.navigate({ minRating: value ? String(value) : null });
  }

  protected toggleBrand(slug: string | null): void {
    if (!slug) {
      return;
    }
    const current = this.selectedBrands();
    const next = current.includes(slug)
      ? current.filter((s) => s !== slug)
      : [...current, slug];
    this.navigate({ brand: next.join('--') || null });
  }

  protected goToPage(page: number): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page > 1 ? page : null },
      queryParamsHandling: 'merge',
    });
  }

  protected clear(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  protected canOrder(product: ProductListItem): boolean {
    return product.isAllowToOrder && (product.stockQuantity ?? 1) !== 0;
  }

  /** Sold-out for ordering purposes; call-for-pricing items are excluded. */
  private isOutOfStock(product: ProductListItem): boolean {
    return !product.isCallForPricing && !this.canOrder(product);
  }

  /** Merges param changes and resets to page 1 for any filter change. */
  private navigate(params: Params): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { ...params, page: null },
      queryParamsHandling: 'merge',
    });
  }

  protected add(product: ProductListItem): void {
    this.cart.add(product).subscribe({
      next: () => this.toast.success(this.translate.instant('product.added')),
      error: () => this.toast.error(this.translate.instant('common.error')),
    });
  }
}
