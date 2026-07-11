import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from 'core';
import { StorefrontFeaturesService, type NewsListItemDto } from 'data-access';
import { Breadcrumb, type BreadcrumbItem, Tile } from 'ui';
import { SeoService } from '../../core/seo.service';

/** The four filter tabs; `slug: null` is the "All" tab. */
const NEWS_TABS: readonly { slug: string | null; key: string }[] = [
  { slug: null, key: 'news.tabs.all' },
  { slug: 'success-story', key: 'news.tabs.success_story' },
  { slug: 'activity', key: 'news.tabs.activity' },
  { slug: 'alert', key: 'news.tabs.alert' },
];

/**
 * News listing (`/news`) — per supported-doc/SUCCESS-STORIES-PAGE.md: an ivory
 * page-header band (breadcrumbs + title + subtitle) over a 3-per-row grid of
 * story cards (1 per row under 980px). Each card is a photo on top of a body
 * with the excerpt and an attribution row (first-letter avatar + date). Data
 * is the published articles from GET /api/news via an httpResource, so the
 * server-rendered route fetches it during SSR and transfers it to the client.
 * Copy is keyed (ar/en) and layout uses logical properties so RTL mirrors.
 */
@Component({
  selector: 'app-news-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, RouterLink, Breadcrumb, Tile],
  templateUrl: './news-list.html',
  styleUrl: './news-list.scss',
})
export class NewsList {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);

  protected readonly tabs = NEWS_TABS;

  /** The active category slug, bound to the `?category=` query param (deep-linkable, SSR-friendly). */
  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });
  protected readonly activeCategory = computed(() => this.queryParams().get('category'));

  protected readonly result = this.service.newsResource(
    () => 1,
    () => this.activeCategory(),
  );
  protected readonly items = computed(() => this.result.value() ?? []);
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));

  // `stream` re-emits on language switch, so crumbs and SEO tags follow the
  // active language (instant() would freeze the first language's strings).
  private readonly homeLabel = toSignal(this.translate.stream('common.home'));
  private readonly titleLabel = toSignal(this.translate.stream('news.title'));
  private readonly subtitleLabel = toSignal(this.translate.stream('news.subtitle'));

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

  protected initial(item: NewsListItemDto): string {
    return item.name?.trim().charAt(0) ?? '';
  }
}
