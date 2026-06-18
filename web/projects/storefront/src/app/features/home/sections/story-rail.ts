import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { NewsListItemDto } from 'data-access';
import { Icon, Tile } from 'ui';

/**
 * Success stories per supported-doc/HOME-PAGE.md §7: section head (eyebrow,
 * title, muted line, "all stories" link to /news) over a 3-column grid of
 * story cards — photo on top, excerpt as the quote, then an attribution row
 * with a first-letter avatar and the article title. Presentational — the Home
 * page passes the first three published articles from GET /api/news. Renders
 * nothing while there are no stories. 1 column under 980px.
 */
@Component({
  selector: 'app-story-rail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, Tile],
  template: `
    @if (items().length) {
      <section class="sec">
        <div class="sec-head">
          <div>
            <span class="eyebrow">{{ 'home.stories_eyebrow' | translate }}</span>
            <h2 class="sec-title">{{ 'home.stories_title' | translate }}</h2>
            <p class="sec-sub">{{ 'home.stories_sub' | translate }}</p>
          </div>
          <a class="sec-link" routerLink="/news">
            {{ 'home.stories_all' | translate }}
            <lib-icon name="arrowEnd" [size]="16" />
          </a>
        </div>

        <div class="stories">
          @for (story of items(); track story.id) {
            <article class="story">
              <a
                class="story-media"
                [routerLink]="['/news', story.slug]"
                [attr.aria-label]="story.name"
              >
                <lib-tile
                  [src]="story.thumbnailUrl"
                  [seed]="story.name ?? story.id"
                  [alt]="story.name"
                  ratio="4x3"
                />
              </a>
              <div class="story-body">
                @if (story.shortContent) {
                  <p class="story-quote">{{ story.shortContent }}</p>
                }
                <div class="story-by">
                  <span class="story-av" aria-hidden="true">{{ initial(story) }}</span>
                  <a class="story-name" [routerLink]="['/news', story.slug]">
                    <b>{{ story.name }}</b>
                  </a>
                </div>
              </div>
            </article>
          }
        </div>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .sec {
      padding-block: clamp(48px, 7vw, 84px) 0;
    }
    .sec-head {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      margin-block-end: 34px;
    }
    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      font-size: 0.82rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      color: var(--accent);
    }
    .eyebrow::before {
      content: '';
      inline-size: 26px;
      block-size: 2px;
      background: currentColor;
    }
    .sec-title {
      margin-block: 10px 0;
      font-weight: 700;
      font-size: clamp(1.6rem, 3.4vw, 2.3rem);
      letter-spacing: -0.02em;
    }
    .sec-sub {
      margin-block: 8px 0;
      color: var(--ink-2);
    }
    .sec-link {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      color: var(--navy);
      font-weight: 700;
      text-decoration: none;
      white-space: nowrap;
    }
    .sec-link:hover {
      color: var(--accent);
    }

    .stories {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 22px;
    }
    @media (max-width: 980px) {
      .stories {
        grid-template-columns: 1fr;
      }
    }
    .story {
      display: flex;
      flex-direction: column;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
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
    .story-quote {
      font-size: 1.04rem;
      line-height: 1.7;
      color: var(--ink);
      margin: 0;
      display: -webkit-box;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 3;
      overflow: hidden;
    }
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
    .story-name {
      text-decoration: none;
      color: var(--ink);
      font-size: 0.95rem;
    }
    .story-name:hover {
      color: var(--accent);
    }
  `,
})
export class StoryRail {
  readonly items = input<readonly NewsListItemDto[]>([]);

  protected initial(item: NewsListItemDto): string {
    return item.name?.trim().charAt(0) ?? '';
  }
}
