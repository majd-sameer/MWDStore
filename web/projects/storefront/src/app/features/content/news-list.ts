import { DatePipe } from '@angular/common';
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
import { StorefrontFeaturesService, type NewsListItemDto } from 'data-access';
import { Breadcrumb, type BreadcrumbItem, Tile } from 'ui';
import { SeoService } from '../../core/seo.service';

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
  template: `
    <div class="pagehead">
      <div class="ph-inner">
        <lib-breadcrumb [items]="crumbs()" />
        <h1 class="ph-title">{{ 'news.title' | translate }}</h1>
        <p class="ph-sub">{{ 'news.subtitle' | translate }}</p>
      </div>
    </div>

    <section class="stories-wrap">
      @if (result.error()) {
        <div class="alert alert-danger">{{ 'common.error' | translate }}</div>
      } @else if (!items().length && result.isLoading()) {
        <div class="state">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      } @else if (items().length) {
        <div class="stories">
          @for (n of items(); track n.id) {
            <article class="story">
              <a class="story-media" [routerLink]="['/news', n.slug]" [attr.aria-label]="n.name">
                <lib-tile
                  [src]="n.thumbnailUrl"
                  [seed]="n.name ?? n.id"
                  [alt]="n.name"
                  ratio="4x3"
                />
              </a>
              <div class="story-body">
                <a class="story-link" [routerLink]="['/news', n.slug]">
                  <h2 class="story-title">{{ n.name }}</h2>
                </a>
                @if (n.shortContent) {
                  <p class="story-quote">{{ n.shortContent }}</p>
                }
                <div class="story-by">
                  <span class="story-av" aria-hidden="true">{{ initial(n) }}</span>
                  <span class="story-meta">
                    @if (n.publishedOn) {
                      <time [attr.datetime]="n.publishedOn">
                        {{ n.publishedOn | date: 'mediumDate' : '' : locale() }}
                      </time>
                    }
                  </span>
                  <a class="story-more" [routerLink]="['/news', n.slug]">
                    {{ 'news.read_more' | translate }}
                  </a>
                </div>
              </div>
            </article>
          }
        </div>
      } @else {
        <p class="empty">{{ 'news.empty' | translate }}</p>
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

    /* ---- Stories grid: 3 per row, 1 per row under 980px ---- */
    .stories-wrap {
      padding-block: 40px;
    }
    .stories {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 22px;
    }

    /* ---- Story card ---- */
    .story {
      display: flex;
      flex-direction: column;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: 20px;
      overflow: hidden;
      box-shadow: var(--shadow-sm);
      transition:
        box-shadow 0.15s ease,
        transform 0.15s ease;
    }
    .story:hover {
      box-shadow: var(--shadow-md);
      transform: translateY(-2px);
    }
    .story-media {
      display: block;
    }
    .story-body {
      display: flex;
      flex-direction: column;
      flex: 1 1 auto;
      padding: 22px;
    }
    .story-link {
      text-decoration: none;
      color: var(--ink);
    }
    .story-title {
      font-size: 1.1rem;
      font-weight: 700;
      line-height: 1.4;
      margin: 0 0 0.5rem;
    }
    .story-link:hover .story-title {
      color: var(--accent);
    }
    .story-quote {
      font-size: 1.04rem;
      line-height: 1.7;
      color: var(--ink);
      margin: 0;
      display: -webkit-box;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 4;
      overflow: hidden;
    }

    /* ---- Attribution row: avatar + date, read-more pushed to the end ---- */
    .story-by {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-block-start: auto;
      padding-block-start: 18px;
    }
    .story-av {
      display: flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
      inline-size: 46px;
      block-size: 46px;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--navy);
      font-weight: 700;
    }
    .story-meta {
      display: flex;
      flex-direction: column;
      font-size: 0.8rem;
      color: var(--ink-2);
    }
    .story-more {
      margin-inline-start: auto;
      font-size: 0.85rem;
      font-weight: 600;
      color: var(--navy);
      text-decoration: none;
      white-space: nowrap;
    }
    .story-more:hover {
      text-decoration: underline;
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

    @media (max-width: 979.98px) {
      .stories {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class NewsList {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly result = this.service.newsResource();
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
