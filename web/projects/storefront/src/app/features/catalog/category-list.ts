import { formatNumber } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from 'core';
import { CatalogService, type CategoryDto } from 'data-access';
import { Breadcrumb, type BreadcrumbItem, Icon, type IconName } from 'ui';
import { SeoService } from '../../core/seo.service';
import { CategoryLabelPipe } from '../../shared/category-label.pipe';

/**
 * Store Sections hub (`/categories`) — the landing the "Store Sections" nav item
 * opens. Mirrors the storefront chrome (ivory page-header band with breadcrumbs +
 * title + subtitle) over a grid of category cards: a round ivory chip with a gold
 * craft glyph, the category name, its live product count and a browse affordance.
 * Each card routes to `/shop?category=<slug>`, so shoppers pick a section first and
 * only then see its products. Categories come from GET /api/catalog/categories
 * (top-level, in-menu); counts come from the catalog search facet. Copy is keyed
 * (ar/en) and layout uses logical properties so RTL mirrors.
 */
@Component({
  selector: 'app-category-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Breadcrumb, Icon, CategoryLabelPipe],
  template: `
    <div class="pagehead">
      <div class="ph-inner">
        <lib-breadcrumb [items]="crumbs()" />
        <h1 class="ph-title">{{ 'sections.title' | translate }}</h1>
        <p class="ph-sub">{{ 'sections.subtitle' | translate }}</p>
      </div>
    </div>

    <section class="sections-wrap">
      @if (result.error()) {
        <div class="alert alert-danger">{{ 'common.error' | translate }}</div>
      } @else if (!categories().length && result.isLoading()) {
        <div class="state">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      } @else if (categories().length) {
        <div class="sections">
          @for (cat of categories(); track cat.id; let i = $index) {
            <a
              class="scard"
              [routerLink]="['/shop']"
              [queryParams]="{ category: cat.slug }"
            >
              <span class="scard-ic"><lib-icon [name]="glyphFor(i)" [size]="28" /></span>
              <b class="scard-name">{{ cat.slug | categoryLabel: cat.name }}</b>
              @if (countLabel(cat.slug); as count) {
                <span class="scard-count tabular-nums">
                  {{ 'sections.count' | translate: { count } }}
                </span>
              }
              <span class="scard-go">
                {{ 'sections.browse' | translate }}
                <lib-icon name="arrowEnd" [size]="16" />
              </span>
            </a>
          }
        </div>
      } @else {
        <p class="empty">{{ 'sections.empty' | translate }}</p>
      }
    </section>
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

    /* ---- Sections grid: 3 per row, 2 under 980px, 1 under 520px ---- */
    .sections-wrap {
      padding-block: 40px;
    }
    .sections {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 22px;
    }
    @media (max-width: 980px) {
      .sections {
        grid-template-columns: repeat(2, 1fr);
      }
    }
    @media (max-width: 520px) {
      .sections {
        grid-template-columns: 1fr;
      }
    }

    /* ---- Section card ---- */
    .scard {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 30px 20px 22px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      text-align: center;
      text-decoration: none;
      color: var(--ink);
      box-shadow: var(--shadow-sm);
      transition:
        transform 0.15s ease,
        box-shadow 0.15s ease;
    }
    .scard:hover {
      transform: translateY(-3px);
      box-shadow: var(--shadow-md);
    }
    .scard-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 68px;
      block-size: 68px;
      margin-block-end: 4px;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--accent);
    }
    .scard-name {
      font-size: 1.05rem;
      font-weight: 700;
    }
    .scard-count {
      font-size: 0.85rem;
      color: var(--ink-3);
    }
    .scard-go {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      margin-block-start: 6px;
      color: var(--navy);
      font-size: 0.9rem;
      font-weight: 700;
    }
    .scard:hover .scard-go {
      color: var(--accent);
    }
    /* Arrow points the reading direction (mirrors in RTL). */
    :host-context([dir='rtl']) .scard-go lib-icon {
      transform: scaleX(-1);
    }

    /* ---- States ---- */
    .state {
      text-align: center;
      padding-block: 4rem;
    }
    .empty {
      text-align: center;
      color: var(--ink-2);
      padding-block: 4rem;
      margin: 0;
    }
  `,
})
export class CategoryList {
  private readonly catalog = inject(CatalogService);
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly result = this.catalog.categoriesResource();

  /** Product counts per category slug, from the catalog search facet (one tiny
   *  page is enough — we only read `filterOption.categories`). */
  private readonly facet = this.catalog.productsResource(() => ({ pageSize: 1 }));

  private readonly counts = computed<Record<string, number>>(() => {
    const map: Record<string, number> = {};
    for (const c of this.facet.value()?.filterOption?.categories ?? []) {
      if (c.slug) {
        map[c.slug] = c.count;
      }
    }
    return map;
  });

  /** Top-level, in-menu categories with a usable name + slug, in admin order. */
  protected readonly categories = computed<CategoryDto[]>(() =>
    (this.result.value() ?? [])
      .filter((c) => c.includeInMenu && c.parentId === null && c.slug && c.name)
      .sort((a, b) => a.displayOrder - b.displayOrder),
  );

  protected readonly locale = computed(() =>
    this.language.lang() === 'ar' ? 'ar' : 'en-US',
  );

  // `stream` re-emits on language switch, so crumbs and SEO tags follow the
  // active language (instant() would freeze the first language's strings).
  private readonly homeLabel = toSignal(this.translate.stream('common.home'));
  private readonly titleLabel = toSignal(this.translate.stream('sections.title'));
  private readonly subtitleLabel = toSignal(this.translate.stream('sections.subtitle'));

  protected readonly crumbs = computed<BreadcrumbItem[]>(() => [
    { label: this.homeLabel() ?? '', link: '/' },
    { label: this.titleLabel() ?? '' },
  ]);

  constructor() {
    effect(() => {
      const title = this.titleLabel();
      if (title) {
        this.seo.update({ title, description: this.subtitleLabel() });
      }
    });
  }

  /** Localized product count for a category (Arabic-Indic digits in ar), or null while unknown. */
  protected countLabel(slug: string | null): string | null {
    if (!slug) {
      return null;
    }
    const count = this.counts()[slug];
    return count === undefined ? null : formatNumber(count, this.locale());
  }

  // The API has no per-category icon, so cards rotate through the craft glyphs.
  private static readonly glyphs: readonly IconName[] = [
    'award',
    'spark',
    'leaf',
    'box',
    'shield',
    'pencil',
  ];

  protected glyphFor(index: number): IconName {
    return CategoryList.glyphs[index % CategoryList.glyphs.length];
  }
}
