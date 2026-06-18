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
import { LanguageService } from 'core';
import { StorefrontFeaturesService, type NewsDetailDto } from 'data-access';
import { SeoService } from '../../core/seo.service';

/** News article (`/news/:slug`): server-rendered; body is trusted admin-authored HTML. */
@Component({
  selector: 'app-news-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, RouterLink],
  template: `
    <div class="container py-4 article">
      <nav class="mb-3">
        <a routerLink="/news">← {{ 'news.back' | translate }}</a>
      </nav>

      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      } @else if (item(); as n) {
        <h1 class="page-title">{{ n.name }}</h1>
        @if (n.publishedOn) {
          <time class="article-date" [attr.datetime]="n.publishedOn">
            {{ n.publishedOn | date: 'mediumDate' : '' : locale() }}
          </time>
        }
        @if (n.thumbnailUrl) {
          <img class="article-hero" [src]="n.thumbnailUrl" [alt]="n.name" />
        }
        <div class="article-body" [innerHTML]="n.fullContent"></div>
      } @else {
        <p class="text-body-secondary text-center py-5">{{ 'common.error' | translate }}</p>
      }
    </div>
  `,
  styles: `
    .article {
      max-inline-size: 48rem;
    }
    .page-title {
      font-size: 1.8rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      margin-block-end: 0.25rem;
    }
    .article-date {
      display: block;
      font-size: 0.85rem;
      color: var(--ink-3, #888);
      margin-block-end: 1rem;
    }
    .article-hero {
      inline-size: 100%;
      border-radius: 0.75rem;
      margin-block-end: 1.25rem;
    }
  `,
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
