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
  StorefrontFeaturesService,
  type ProductListItem,
} from 'data-access';
import { ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';
import { SeoService } from '../../core/seo.service';
import { Hero } from './sections/hero';
import { TrustStrip } from './sections/trust-strip';
import { CollectionRail } from './sections/collection-rail';
import { SignatureRail } from './sections/signature-rail';
import { FeaturedRow } from './sections/featured-row';
import { MissionBand } from './sections/mission-band';
import { StoryRail } from './sections/story-rail';
import { ValuesRow } from './sections/values-row';
import { CtaBand } from './sections/cta-band';
import { AlertBand } from './sections/alert-band/alert-band';

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
    AlertBand,
    Hero,
    TrustStrip,
    CollectionRail,
    SignatureRail,
    FeaturedRow,
    MissionBand,
    StoryRail,
    ValuesRow,
    CtaBand,
  ],
  templateUrl: './home.html',
})
export class Home {
  private readonly catalog = inject(CatalogService);
  private readonly features = inject(StorefrontFeaturesService);
  private readonly cart = inject(CartStore);
  private readonly toast = inject(ToastService);
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);

  protected readonly categories = this.catalog.categoriesResource();
  private readonly vendorCount = this.catalog.vendorCountResource();
  /** Curated signature products for the rail above best sellers. */
  protected readonly signature = this.catalog.signatureResource(() => 8);
  /** Top-rated products stand in for "best sellers" (the API has no sales rank). */
  protected readonly best = this.catalog.productsResource(() => ({
    pageSize: 4,
    sort: 'rating',
  }));
  protected readonly fresh = this.catalog.productsResource(() => ({
    pageSize: 4,
    sort: 'newest',
  }));
  /** Latest success stories drive the story rail; alerts drive the top announcement band. */
  private readonly news = this.features.newsResource(() => 1, () => 'success-story');
  protected readonly alerts = this.features.alertsResource();

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
