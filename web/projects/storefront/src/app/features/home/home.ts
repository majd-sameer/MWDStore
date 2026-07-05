import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import {
  CatalogService,
  ContentService,
  StorefrontFeaturesService,
  type ContentBlockDto,
  type ProductListItem,
} from 'data-access';
import { ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';
import { JsonLdService } from '../../core/json-ld.service';
import { SeoService } from '../../core/seo.service';
import { Hero } from './sections/hero';
import { TrustStrip } from './sections/trust-strip';
import { CollectionRail } from './sections/collection-rail';
import { FeaturedRow } from './sections/featured-row';
import { MissionBand } from './sections/mission-band';
import { StoryRail } from './sections/story-rail';
import { ValuesRow } from './sections/values-row';
import { CtaBand } from './sections/cta-band';

/**
 * Home page per supported-doc/HOME-PAGE.md: hero → trust strip → categories →
 * best sellers → mission band → new arrivals → success stories → values → CTA
 * band. Everything dynamic comes from the API via httpResources (SSR-rendered
 * and transfer-cached): categories + per-category counts (catalog search
 * facet), best sellers (top rated), new arrivals (newest), stories (news), and
 * the hero stats (centers = active vendors, total product count).
 */
@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    Hero,
    TrustStrip,
    CollectionRail,
    FeaturedRow,
    MissionBand,
    StoryRail,
    ValuesRow,
    CtaBand,
  ],
  template: `
    <app-hero
      [centers]="centerCount()"
      [products]="productCount()"
      [block]="heroBlock()"
    />

    <app-trust-strip />

    <app-collection-rail
      [categories]="categories.value() ?? []"
      [counts]="categoryCounts()"
    />

    <app-featured-row
      eyebrow="home.best_eyebrow"
      title="home.best_title"
      [products]="best.value()?.products ?? []"
      (addToCart)="add($event)"
    />

    <app-mission-band [block]="storyBlock()" [valueBlocks]="valueBlocks()" />

    <app-featured-row
      eyebrow="home.fresh_eyebrow"
      title="home.fresh_title"
      [products]="fresh.value()?.products ?? []"
      (addToCart)="add($event)"
    />

    <app-story-rail [items]="stories()" />

    <app-values-row [blocks]="valueBlocks()" />

    <app-cta-band [block]="ctaBlock()" />
  `,
})
export class Home {
  private readonly catalog = inject(CatalogService);
  private readonly content = inject(ContentService);
  private readonly features = inject(StorefrontFeaturesService);
  private readonly cart = inject(CartStore);
  private readonly toast = inject(ToastService);
  private readonly seo = inject(SeoService);
  private readonly jsonLd = inject(JsonLdService);
  private readonly translate = inject(TranslateService);

  protected readonly categories = this.catalog.categoriesResource();

  /** Admin-editable homepage content blocks (`home.hero`, `home.story`, `home.value.1..5`,
   * `home.cta`). Missing/unpublished keys resolve to `null` — sections fall back to their
   * built-in i18n copy so nothing ever renders blank. */
  private readonly contentBlocks = this.content.blocksResource(() => 'home');
  private readonly blocksByKey = computed(() => {
    const map = new Map<string, ContentBlockDto>();
    for (const block of this.contentBlocks.value() ?? []) {
      map.set(block.key, block);
    }
    return map;
  });
  protected readonly heroBlock = computed(() => this.blocksByKey().get('home.hero') ?? null);
  protected readonly storyBlock = computed(() => this.blocksByKey().get('home.story') ?? null);
  protected readonly ctaBlock = computed(() => this.blocksByKey().get('home.cta') ?? null);
  /** The five "our values" blocks (`home.value.1`..`home.value.5`), in order — shared by
   * `MissionBand` (first four) and `ValuesRow` (all five). */
  protected readonly valueBlocks = computed(() => {
    const map = this.blocksByKey();
    const blocks: ContentBlockDto[] = [];
    for (let i = 1; i <= 5; i++) {
      const block = map.get(`home.value.${i}`);
      if (block) {
        blocks.push(block);
      }
    }
    return blocks;
  });
  private readonly vendorCount = this.catalog.vendorCountResource();
  /** Top-rated products stand in for "best sellers" (the API has no sales rank). */
  protected readonly best = this.catalog.productsResource(() => ({
    pageSize: 4,
    sort: 'rating',
  }));
  protected readonly fresh = this.catalog.productsResource(() => ({
    pageSize: 4,
    sort: 'newest',
  }));
  private readonly news = this.features.newsResource();

  /** Hero stat: reform & rehabilitation centers (active vendors on the API). */
  protected readonly centerCount = computed(() => this.vendorCount.value() ?? null);

  /** Hero stat: total handmade products in the catalog. */
  protected readonly productCount = computed(
    () => this.best.value()?.totalProduct ?? null,
  );

  /** Product count per category id, from the catalog search facet. */
  protected readonly categoryCounts = computed<Readonly<Record<number, number>>>(
    () => {
      const facets = this.best.value()?.filterOption?.categories ?? [];
      return Object.fromEntries(facets.map((f) => [f.id, f.count]));
    },
  );

  /** First three published articles as the success-story cards. */
  protected readonly stories = computed(() => (this.news.value() ?? []).slice(0, 3));

  // `stream` re-emits on language switch, so the SEO tags follow the active
  // language (instant() would freeze the first language's strings).
  private readonly metaTitle = toSignal(this.translate.stream('home.meta_title'));
  private readonly metaDescription = toSignal(
    this.translate.stream('home.meta_description'),
  );

  constructor() {
    effect(() => {
      const title = this.metaTitle();
      if (title) {
        this.seo.update({ title, description: this.metaDescription() });
        // No product schema applies on the home page — drop a stale one left over
        // from a client-side navigation back from a product page.
        this.jsonLd.remove('product');
        this.jsonLd.set('breadcrumb', {
          '@type': 'BreadcrumbList',
          itemListElement: [
            { '@type': 'ListItem', position: 1, name: this.translate.instant('common.home') },
          ],
        });
      }
    });
  }

  protected add(product: ProductListItem): void {
    this.cart.add(product).subscribe({
      next: () => this.toast.success(this.translate.instant('product.added')),
      error: () => this.toast.error(this.translate.instant('common.error')),
    });
  }
}
