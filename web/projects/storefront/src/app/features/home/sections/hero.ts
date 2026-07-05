import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from 'core';
import type { ContentBlockDto } from 'data-access';
import { Button, Icon } from 'ui';

/**
 * Home hero per supported-doc/HOME-PAGE.md §1: ivory band with a soft gold
 * glow, 2-column grid — copy (eyebrow, gold-accented H1, lead, green + ghost
 * CTAs, 3 stats) beside a 4:5 photo with a floating impact badge. The centers /
 * products stats are fed from `data-access` by the Home page; the proceeds
 * figure is brand copy. All copy keyed through ngx-translate; numerals follow
 * the active locale (Arabic-Indic in ar).
 *
 * The title, lead paragraph, photo and shop CTA are admin-editable via the
 * `home.hero` content block (`[block]`, from `ContentService`) — when present
 * and published it overrides the i18n copy below; missing/unpublished falls
 * back gracefully to the original hardcoded copy.
 */
@Component({
  selector: 'app-hero',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, TranslatePipe, Button, Icon],
  template: `
    <section class="hero">
      <div class="hero-deco" aria-hidden="true"></div>
      <div class="wrap hero-grid">
        <div class="hero-copy">
          <span class="eyebrow">{{ 'home.hero_eyebrow' | translate }}</span>
          <h1 class="hero-title">
            @if (block()?.title; as title) {
              {{ title }}
            } @else {
              {{ 'home.hero_title_pre' | translate }}
              <span class="accent">{{ 'home.hero_title_accent' | translate }}</span>
              <br />
              {{ 'home.hero_title_post' | translate }}
            }
          </h1>
          <p class="hero-lead">{{ block()?.text || ('home.hero_lead' | translate) }}</p>

          <div class="hero-ctas">
            <a libButton variant="primary" size="lg" [routerLink]="block()?.linkUrl || '/shop'">
              {{ block()?.linkText || ('home.hero_cta_shop' | translate) }}
              <lib-icon name="arrowEnd" [size]="18" class="ms-1" />
            </a>
            <a
              libButton
              variant="secondary"
              [outline]="true"
              size="lg"
              routerLink="/pages/about-us"
            >
              {{ 'home.hero_cta_story' | translate }}
            </a>
          </div>

          <dl class="hero-stats">
            <div class="stat">
              <dt class="stat-val tabular-nums">
                {{ (centers() ?? 0) | number: '' : locale() }}
              </dt>
              <span class="stat-label">{{ 'home.hero_stat_centers' | translate }}</span>
            </div>
            <div class="stat">
              <dt class="stat-val tabular-nums">
                +{{ (products() ?? 0) | number: '' : locale() }}
              </dt>
              <span class="stat-label">{{ 'home.hero_stat_products' | translate }}</span>
            </div>
            <div class="stat">
              <dt class="stat-val tabular-nums">
                {{ 'home.hero_stat_proceeds_v' | translate }}
              </dt>
              <span class="stat-label">{{ 'home.hero_stat_proceeds' | translate }}</span>
            </div>
          </dl>
        </div>

        <div class="hero-media">
          <img
            class="hero-photo"
            [src]="block()?.imageUrl || '/home-hero.jpg'"
            [alt]="'home.hero_photo' | translate"
            width="900"
            height="1125"
            fetchpriority="high"
          />
          <div class="hero-badge">
            <span class="badge-ic"><lib-icon name="hands" [size]="22" /></span>
            <span class="badge-txt">
              <b>{{ 'home.hero_badge_t' | translate }}</b>
              <span>{{ 'home.hero_badge_s' | translate }}</span>
            </span>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    /* Full-bleed ivory band, flush under the header (cancels .app-main's top pad). */
    .hero {
      position: relative;
      overflow: hidden;
      margin-inline: calc(50% - 50vw);
      margin-block-start: -2.5rem;
      background: var(--canvas);
    }
    .hero-deco {
      position: absolute;
      inset-block-start: -160px;
      inset-inline-end: -160px;
      inline-size: 480px;
      block-size: 480px;
      background: radial-gradient(circle, rgba(201, 151, 30, 0.22), transparent 70%);
      pointer-events: none;
    }
    .hero-grid {
      position: relative;
      display: grid;
      grid-template-columns: 1.05fr 0.95fr;
      gap: 48px;
      align-items: center;
      padding-block: clamp(40px, 6vw, 80px);
    }
    @media (max-width: 980px) {
      .hero-grid {
        grid-template-columns: 1fr;
        gap: 36px;
      }
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
    .hero-title {
      margin-block: 14px 0;
      font-weight: 700;
      font-size: clamp(2.1rem, 5vw, 3.6rem);
      line-height: 1.12;
      color: var(--ink);
    }
    .hero-title .accent {
      color: var(--accent);
    }
    .hero-lead {
      margin-block: 18px 0;
      max-inline-size: 52ch;
      font-size: clamp(1.05rem, 1.6vw, 1.22rem);
      color: var(--ink-2);
    }
    .hero-ctas {
      display: grid;
      grid-auto-flow: column;
      grid-auto-columns: 1fr;
      justify-content: start;
      gap: 0.75rem;
      inline-size: fit-content;
      max-inline-size: 100%;
      margin-block-start: 28px;
    }
    .hero-ctas > a {
      inline-size: 100%;
      justify-content: center;
    }
    /* Stack full-width (still equal) once the row gets tight. */
    @media (max-width: 540px) {
      .hero-ctas {
        grid-auto-flow: row;
        inline-size: 100%;
      }
    }
    .hero-stats {
      display: flex;
      gap: 2.5rem;
      margin-block: 2.5rem 0;
    }
    .stat-val {
      font-size: 1.9rem;
      font-weight: 700;
      color: var(--navy);
      line-height: 1;
    }
    .stat-label {
      margin-block-start: 0.35rem;
      font-size: 0.85rem;
      color: var(--ink-3);
    }

    .hero-media {
      position: relative;
    }
    .hero-photo {
      display: block;
      aspect-ratio: 4 / 5;
      max-block-size: 540px;
      inline-size: 100%;
      block-size: auto;
      object-fit: cover;
      border-radius: var(--r-xl);
      box-shadow: var(--shadow-md);
    }
    .hero-badge {
      position: absolute;
      inset-block-end: -18px;
      inset-inline-start: -10px;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px 18px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      box-shadow: var(--shadow-md);
      max-inline-size: 280px;
    }
    .badge-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
      inline-size: 44px;
      block-size: 44px;
      border-radius: 50%;
      background: var(--green-soft);
      color: var(--green-strong);
    }
    .badge-txt {
      display: flex;
      flex-direction: column;
      line-height: 1.35;
    }
    .badge-txt b {
      font-size: 0.95rem;
      color: var(--ink);
    }
    .badge-txt span {
      font-size: 0.8rem;
      color: var(--ink-2);
    }
  `,
})
export class Hero {
  private readonly language = inject(LanguageService);

  /** Number of reform & rehabilitation centers (active vendor count from the API). */
  readonly centers = input<number | null>(null);
  /** Total handmade products in the catalog (from the API). */
  readonly products = input<number | null>(null);
  /** The `home.hero` content block, or null when missing/unpublished (falls back to i18n). */
  readonly block = input<ContentBlockDto | null>(null);

  protected readonly locale = computed(() =>
    this.language.lang() === 'ar' ? 'ar' : 'en-US',
  );
}
