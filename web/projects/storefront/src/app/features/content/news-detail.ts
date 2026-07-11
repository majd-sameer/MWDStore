import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import { StorefrontFeaturesService, type NewsDetailDto } from 'data-access';
import { Button, Tile } from 'ui';
import { SeoService } from '../../core/seo.service';

/** Maps a category slug to its badge label key (null for uncategorised / unknown). */
const BADGE_KEYS: Readonly<Record<string, string>> = {
  'success-story': 'news.badge.success_story',
  activity: 'news.badge.activity',
  alert: 'news.badge.alert',
};

/** News article (`/news/:slug`): server-rendered; body is trusted admin-authored HTML. */
@Component({
  selector: 'app-news-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, RouterLink, Button, Tile, MoneyPipe],
  templateUrl: './news-detail.html',
  styleUrl: './news-detail.scss',
})
export class NewsDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(StorefrontFeaturesService);
  private readonly seo = inject(SeoService);
  private readonly language = inject(LanguageService);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly item = signal<NewsDetailDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));

  /** The i18n key for the category badge, or null when there's nothing to show. */
  protected badgeKey(slug: string | null): string | null {
    return slug ? (BADGE_KEYS[slug] ?? null) : null;
  }

  constructor() {
    effect(() => {
      const slug = this.params().get('slug');
      if (!slug) {
        return;
      }
      this.loading.set(true);
      this.service.newsDetail(slug).subscribe({
        next: (item) => {
          this.item.set(item);
          this.loading.set(false);
          this.seo.update({
            title: item.metaTitle || item.name || slug,
            description: item.metaDescription ?? item.shortContent ?? undefined,
            image: item.thumbnailUrl ?? undefined,
            type: 'article',
          });
        },
        error: () => {
          this.item.set(null);
          this.loading.set(false);
        },
      });
    });
  }
}
