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
  templateUrl: './category-list.html',
  styleUrl: './category-list.scss',
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
