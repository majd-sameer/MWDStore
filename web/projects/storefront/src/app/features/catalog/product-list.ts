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
import { JsonLdService } from '../../core/json-ld.service';
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
  template: `
    <div class="pagehead">
      <div class="ph-inner">
        <lib-breadcrumb [items]="crumbs()" />
        <h1 class="ph-title">{{ title() }}</h1>
        <p class="ph-sub">
          {{ (query().query ? 'shop.search_sub' : 'shop.subtitle') | translate }}
        </p>
      </div>
    </div>

    <div class="listing">
      <aside class="listing-side">
        <ng-container *ngTemplateOutlet="filtersTpl; context: { ns: 'side' }" />
      </aside>

      <div class="listing-main">
        <div class="toolbar">
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm filter-toggle"
            (click)="sheetOpen.set(true)"
          >
            <lib-icon name="filter" [size]="18" />
            {{ 'shop.filter' | translate }}
          </button>

          <span class="result-n">
            {{
              (totalProduct() === 1 ? 'shop.result' : 'shop.results')
                | translate: { count: totalProduct() }
            }}
          </span>

          <div class="toolbar-right">
            <div class="viewtoggle" role="group" [attr.aria-label]="'shop.view' | translate">
              <button
                type="button"
                [class.is-on]="view() === 'grid'"
                (click)="view.set('grid')"
                [attr.aria-label]="'shop.view_grid' | translate"
              >
                <lib-icon name="grid" [size]="18" />
              </button>
              <button
                type="button"
                [class.is-on]="view() === 'list'"
                (click)="view.set('list')"
                [attr.aria-label]="'shop.view_list' | translate"
              >
                <lib-icon name="menu" [size]="18" />
              </button>
            </div>

            <label class="sortsel">
              <span class="sort-label">{{ 'common.sort' | translate }}:</span>
              <select
                class="form-select form-select-sm"
                [value]="query().sort ?? ''"
                (change)="setSort($event)"
                [attr.aria-label]="'common.sort' | translate"
              >
                <option value="">{{ 'common.featured' | translate }}</option>
                <option value="price-asc">{{ 'common.price_low' | translate }}</option>
                <option value="price-desc">{{ 'common.price_high' | translate }}</option>
                <option value="rating">{{ 'common.rating' | translate }}</option>
              </select>
            </label>
          </div>
        </div>

        @if (result.error()) {
          <div class="alert alert-danger">{{ 'common.error' | translate }}</div>
        } @else if (!data() && result.isLoading()) {
          <div class="state">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (data(); as r) {
          <div [class.is-refreshing]="result.isLoading()">
            @if (r.products?.length) {
              @if (effectiveView() === 'grid') {
                <div class="pgrid">
                  @for (product of products(); track product.id) {
                    <app-product-card [product]="product" (addToCart)="add($event)" />
                  }
                </div>
              } @else {
                <div class="listcol">
                  @for (p of products(); track p.id) {
                    <article class="lrow">
                      <a
                        class="lrow-media"
                        [routerLink]="['/products', p.id]"
                        [attr.aria-label]="p.name"
                      >
                        <lib-tile
                          [src]="p.thumbnailImageUrl"
                          [seed]="p.name ?? p.id"
                          [alt]="p.name"
                          ratio="4x3"
                        />
                      </a>
                      <div class="lrow-body">
                        @if (p.categoryName) {
                          <span class="lrow-cat">{{ p.categorySlug | categoryLabel: p.categoryName }}</span>
                        }
                        <a class="lrow-title" [routerLink]="['/products', p.id]">
                          <h3>{{ p.name }}</h3>
                        </a>
                        @if (p.shortDescription) {
                          <p class="lrow-desc">{{ p.shortDescription }}</p>
                        }
                        @if (p.ratingAverage; as rating) {
                          <lib-stars [rating]="rating" [count]="p.reviewsCount" />
                        }
                      </div>
                      <div class="lrow-buy">
                        <div class="lrow-price">
                          @if (p.isCallForPricing) {
                            <span class="lrow-call">
                              {{ 'product.call_for_pricing' | translate }}
                            </span>
                          } @else {
                            <span class="lrow-now tabular-nums">
                              {{ p.calculatedProductPrice.price | money }}
                            </span>
                            @if (p.calculatedProductPrice.oldPrice; as old) {
                              <span class="lrow-old tabular-nums">{{ old | money }}</span>
                            }
                          }
                        </div>
                        @if (!p.isCallForPricing) {
                          <button
                            type="button"
                            class="btn btn-primary btn-sm lrow-add"
                            [disabled]="!canOrder(p)"
                            (click)="add(p)"
                          >
                            <lib-icon name="bag" [size]="16" />
                            {{ 'shop.add_to_cart' | translate }}
                          </button>
                        }
                      </div>
                    </article>
                  }
                </div>
              }

              <div class="pager">
                <lib-pagination
                  [page]="r.page"
                  [pageSize]="r.pageSize"
                  [collectionSize]="r.totalProduct"
                  (pageChange)="goToPage($event)"
                />
              </div>
            } @else {
              <div class="empty">
                <lib-icon name="search" [size]="40" />
                <h3>{{ 'shop.empty_title' | translate }}</h3>
                <p>{{ 'shop.empty_sub' | translate }}</p>
                <button type="button" class="btn btn-outline-secondary" (click)="clear()">
                  {{ 'shop.reset_filters' | translate }}
                </button>
              </div>
            }
          </div>
        }
      </div>
    </div>

    <!-- Mobile filter sheet (same filters markup, slides in below 980px) -->
    <div class="filtersheet" [class.is-open]="sheetOpen()">
      <button
        type="button"
        class="sheet-scrim"
        (click)="sheetOpen.set(false)"
        [attr.aria-label]="'shop.close' | translate"
        [attr.tabindex]="sheetOpen() ? null : -1"
      ></button>
      <div class="sheet-panel">
        <div class="sheet-head">
          <h3>{{ 'shop.filter' | translate }}</h3>
          <button
            type="button"
            class="sheet-close"
            (click)="sheetOpen.set(false)"
            [attr.aria-label]="'shop.close' | translate"
          >
            <lib-icon name="x" [size]="20" />
          </button>
        </div>
        <ng-container *ngTemplateOutlet="filtersTpl; context: { ns: 'sheet' }" />
        <button
          type="button"
          class="btn btn-primary w-100 sheet-show"
          (click)="sheetOpen.set(false)"
        >
          {{ 'shop.show_products' | translate: { count: totalProduct() } }}
        </button>
      </div>
    </div>

    <!-- Filters card — rendered in the sidebar and again inside the mobile
         sheet; ns namespaces the radio groups so the two copies don't fight. -->
    <ng-template #filtersTpl let-ns="ns">
      <div class="filters">
        <div class="filters-head">
          <h3>{{ 'shop.filter' | translate }}</h3>
          @if (hasFilters()) {
            <button type="button" class="link-btn" (click)="clear()">
              {{ 'shop.reset' | translate }}
            </button>
          }
        </div>

        <div class="fgroup">
          <h4>{{ 'shop.category' | translate }}</h4>
          <div class="frows">
            <label class="frow" [class.is-on]="!query().category">
              <input
                type="radio"
                [name]="'cat-' + ns"
                [checked]="!query().category"
                (change)="setCategory(null)"
              />
              <span class="frow-name">{{ 'shop.all_products' | translate }}</span>
              <span class="frow-n tabular-nums">{{ allCount() }}</span>
            </label>
            @for (cat of categories(); track cat.id) {
              <label class="frow" [class.is-on]="query().category === cat.slug">
                <input
                  type="radio"
                  [name]="'cat-' + ns"
                  [checked]="query().category === cat.slug"
                  (change)="setCategory(cat.slug)"
                />
                <span class="frow-name">{{ cat.slug | categoryLabel: cat.name }}</span>
                <span class="frow-n tabular-nums">{{ cat.count }}</span>
              </label>
            }
          </div>
        </div>

        @if (priceFacet()) {
          <div class="fgroup">
            <h4>{{ 'common.max_price' | translate }}</h4>
            <input
              type="range"
              class="form-range"
              [min]="sliderMin()"
              [max]="sliderMax()"
              step="1"
              [value]="sliderValue()"
              (change)="applyMaxPrice($event)"
              [attr.aria-label]="'common.max_price' | translate"
            />
            <div class="frange">
              <span class="tabular-nums">{{ sliderMin() | money }}</span>
              <span class="tabular-nums">{{ sliderValue() | money }}</span>
            </div>
          </div>
        }

        @if (brands().length) {
          <div class="fgroup">
            <h4>{{ 'shop.center' | translate }}</h4>
            <div class="frows">
              @for (b of brands(); track b.id) {
                <label class="frow" [class.is-on]="selectedBrands().includes(b.slug ?? '')">
                  <input
                    type="checkbox"
                    [checked]="selectedBrands().includes(b.slug ?? '')"
                    (change)="toggleBrand(b.slug)"
                  />
                  <span class="frow-name">{{ b.name }}</span>
                  <span class="frow-n tabular-nums">{{ b.count }}</span>
                </label>
              }
            </div>
          </div>
        }

        <div class="fgroup">
          <h4>{{ 'shop.rating' | translate }}</h4>
          <div class="frows">
            @for (r of ratingOptions; track r) {
              <label class="frow" [class.is-on]="query().minRating === r">
                <input
                  type="radio"
                  [name]="'rating-' + ns"
                  [checked]="query().minRating === r"
                  (change)="setRating(r)"
                />
                <span class="frow-name frow-stars">
                  <lib-stars [rating]="r" />
                  {{ 'shop.rating_min' | translate: { value: r } }}
                </span>
              </label>
            }
            <label class="frow" [class.is-on]="!query().minRating">
              <input
                type="radio"
                [name]="'rating-' + ns"
                [checked]="!query().minRating"
                (change)="setRating(null)"
              />
              <span class="frow-name">{{ 'shop.all_ratings' | translate }}</span>
            </label>
          </div>
        </div>
      </div>
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
    }

    /* ---- Page header band — full-bleed breakout from the .wrap container ---- */
    .pagehead {
      margin-inline: calc(50% - 50vw);
      margin-block-start: -2.5rem; /* cancel .app-main's top padding so the band hugs the header */
      background: var(--surface-2);
      border-block-end: 1px solid var(--line);
    }
    .ph-inner {
      max-inline-size: var(--maxw);
      margin-inline: auto;
      padding-inline: 32px;
      padding-block: 1.9rem 2.1rem;
    }
    .ph-title {
      font-weight: 700;
      font-size: clamp(1.7rem, 3.2vw, 2.4rem);
      letter-spacing: -0.02em;
      margin-block: 0.35rem 0;
    }
    .ph-sub {
      color: var(--ink-2);
      margin-block: 0.35rem 0;
    }

    /* ---- 2-column listing: sidebar inline-start (right in RTL), products fill ---- */
    .listing {
      display: grid;
      grid-template-columns: 268px 1fr;
      gap: 34px;
      align-items: start;
      padding-block: 2rem 3rem;
    }
    .listing-side {
      position: sticky;
      inset-block-start: 90px;
    }

    /* ---- Filters card ---- */
    .filters {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 22px;
      box-shadow: var(--shadow-sm);
    }
    .filters-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .filters-head h3 {
      font-size: 1.05rem;
      font-weight: 700;
      margin: 0;
    }
    .link-btn {
      border: 0;
      background: none;
      color: var(--navy);
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      padding: 0;
    }
    .link-btn:hover {
      text-decoration: underline;
    }
    .fgroup {
      border-block-start: 1px solid var(--line-2);
      margin-block-start: 1rem;
      padding-block-start: 1rem;
    }
    .fgroup h4 {
      font-size: 0.85rem;
      font-weight: 700;
      color: var(--ink-2);
      margin-block: 0 0.6rem;
    }
    .frows {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
    }
    .frow {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      padding-block: 0.4rem;
      padding-inline: 0.55rem;
      border-radius: var(--r-sm);
      cursor: pointer;
      font-size: 0.92rem;
      color: var(--ink-2);
      margin: 0;
    }
    .frow:hover {
      background: var(--surface-2);
    }
    .frow.is-on {
      background: var(--green-soft);
      color: var(--ink);
      font-weight: 600;
    }
    .frow input {
      accent-color: var(--green);
      flex: 0 0 auto;
    }
    .frow-name {
      flex: 1 1 auto;
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      min-inline-size: 0;
    }
    .frow-n {
      border-radius: 999px;
      font-size: 0.72rem;
      padding-inline: 0.5rem;
      padding-block: 0.1rem;
      background: var(--surface-3);
      color: var(--ink-2);
    }
    .frow.is-on .frow-n {
      background: var(--navy);
      color: #fff;
    }
    .frange {
      display: flex;
      justify-content: space-between;
      color: var(--ink-2);
      font-size: 0.85rem;
    }

    /* ---- Toolbar ---- */
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1rem;
      margin-block-end: 1.4rem;
    }
    .filter-toggle {
      display: none;
      align-items: center;
      gap: 0.4rem;
    }
    .result-n {
      color: var(--ink-2);
    }
    .toolbar-right {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-inline-start: auto;
    }
    .viewtoggle {
      display: inline-flex;
      border: 1px solid var(--line);
      border-radius: var(--r-sm);
      overflow: hidden;
    }
    /* Always card view on mobile — drop the grid/list toggle. */
    @media (max-width: 542px) {
      .viewtoggle {
        display: none;
      }
    }
    .viewtoggle button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 38px;
      block-size: 34px;
      border: 0;
      background: var(--surface);
      color: var(--ink-2);
      cursor: pointer;
    }
    .viewtoggle button.is-on {
      background: var(--navy);
      color: #fff;
    }
    .sortsel {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
    }
    .sort-label {
      color: var(--ink-2);
      font-size: 0.9rem;
      white-space: nowrap;
    }
    .sortsel .form-select {
      inline-size: auto;
    }

    /* ---- Grid view ---- */
    .pgrid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 22px;
    }

    /* ---- List view ---- */
    .listcol {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .lrow {
      display: grid;
      grid-template-columns: 170px 1fr auto;
      gap: 16px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 16px;
      transition:
        box-shadow 0.15s ease,
        transform 0.15s ease;
    }
    .lrow:hover {
      box-shadow: var(--shadow-md);
      transform: translateY(-2px);
    }
    .lrow-media {
      display: block;
    }
    .lrow-body {
      min-inline-size: 0;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
    }
    .lrow-cat {
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--accent);
    }
    .lrow-title {
      text-decoration: none;
      color: var(--ink);
    }
    .lrow-title h3 {
      font-size: 1.05rem;
      font-weight: 600;
      margin-block: 0.15rem;
    }
    .lrow-title:hover h3 {
      color: var(--accent);
    }
    .lrow-desc {
      color: var(--ink-2);
      font-size: 0.9rem;
      margin-block: 0.25rem 0.4rem;
      display: -webkit-box;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      overflow: hidden;
    }
    .lrow-buy {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      justify-content: space-between;
      gap: 0.75rem;
    }
    .lrow-price {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
    }
    .lrow-now {
      font-weight: 700;
      font-size: 1.1rem;
    }
    .lrow-old {
      color: var(--ink-3);
      text-decoration: line-through;
      font-size: 0.85rem;
    }
    .lrow-call {
      font-size: 0.85rem;
      color: var(--ink-2);
    }
    .lrow-add {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      white-space: nowrap;
    }

    /* ---- States ---- */
    .state {
      text-align: center;
      padding-block: 4rem;
    }
    .is-refreshing {
      opacity: 0.55;
      pointer-events: none;
    }
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.65rem;
      text-align: center;
      padding-block: 4rem;
      color: var(--ink-2);
    }
    .empty h3 {
      color: var(--ink);
      margin: 0;
    }
    .empty p {
      margin: 0 0 0.5rem;
    }
    .pager {
      display: flex;
      justify-content: center;
      margin-block-start: 2.5rem;
    }

    /* ---- Mobile filter sheet ---- */
    .filtersheet {
      position: fixed;
      inset: 0;
      z-index: 1050;
      visibility: hidden;
      pointer-events: none;
    }
    .filtersheet.is-open {
      visibility: visible;
      pointer-events: auto;
    }
    .sheet-scrim {
      position: absolute;
      inset: 0;
      border: 0;
      padding: 0;
      background: rgba(20, 24, 25, 0.45);
      opacity: 0;
      transition: opacity 0.2s ease;
    }
    .filtersheet.is-open .sheet-scrim {
      opacity: 1;
    }
    .sheet-panel {
      position: absolute;
      inset-block: 0;
      inset-inline-start: 0;
      inline-size: min(88vw, 360px);
      background: var(--canvas);
      padding: 16px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 1rem;
      transform: translateX(-100%);
      transition: transform 0.25s ease;
    }
    .filtersheet.is-open .sheet-panel {
      transform: translateX(0);
    }
    :host-context([dir='rtl']) .sheet-panel {
      transform: translateX(100%);
    }
    :host-context([dir='rtl']) .filtersheet.is-open .sheet-panel {
      transform: translateX(0);
    }
    .sheet-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .sheet-head h3 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 700;
    }
    .sheet-close {
      border: 0;
      background: none;
      color: var(--ink-2);
      cursor: pointer;
    }
    .sheet-show {
      margin-block-start: auto;
    }

    /* ---- Responsive ---- */
    @media (max-width: 979.98px) {
      .listing {
        grid-template-columns: 1fr;
        gap: 0;
      }
      .listing-side {
        display: none;
      }
      .filter-toggle {
        display: inline-flex;
      }
    }
    @media (max-width: 599.98px) {
      .lrow {
        grid-template-columns: 1fr;
      }
      .lrow-buy {
        flex-direction: row;
        align-items: center;
      }
    }
  `,
})
export class ProductList {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cart = inject(CartStore);
  private readonly seo = inject(SeoService);
  private readonly jsonLd = inject(JsonLdService);
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

  /** Distinct catalog total for the "all products" row (a product in several
   *  categories would be double-counted by summing the per-category counts). */
  protected readonly allCount = computed(
    () =>
      this.data()?.filterOption?.total ??
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
      // No product-detail schema applies here — drop a stale one left over from a
      // client-side navigation away from a product page.
      this.jsonLd.remove('product');
      this.jsonLd.set('breadcrumb', {
        '@type': 'BreadcrumbList',
        itemListElement: this.crumbs().map((item, index) => ({
          '@type': 'ListItem',
          position: index + 1,
          name: item.label,
          ...(item.link ? { item: this.seo.toAbsoluteUrl(item.link) } : {}),
        })),
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
