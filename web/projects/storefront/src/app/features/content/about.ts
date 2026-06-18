import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';
import { SeoService } from '../../core/seo.service';

/**
 * About / من نحن (`/pages/about-us`) — per supported-doc/ABOUT-PAGE.md.
 * Static editorial page: all copy is hardcoded translations (`about.*` keys),
 * no backend call. Three stacked sections: full-bleed navy hero, "how we
 * work" numbered steps, and the five value cards. The layout is built with
 * logical properties so the RTL design mirrors to LTR automatically.
 */
@Component({
  selector: 'app-about',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  template: `
    <!-- Section 1 — navy hero band (full-bleed: breaks out of .app-main's .wrap) -->
    <section class="hero">
      <div class="wrap hero-grid">
        <div>
          <span class="eyebrow">{{ 'about.eyebrow' | translate }}</span>
          <h1 class="hero-title">{{ 'about.hero_title' | translate }}</h1>
          <p class="hero-body">{{ 'about.hero_body' | translate }}</p>
          <a routerLink="/shop" class="hero-cta">
            {{ 'about.hero_cta' | translate }}
            <lib-icon name="arrowEnd" [size]="18" />
          </a>
        </div>
        <img
          class="hero-photo"
          src="/about-us.jpg"
          [alt]="'about.hero_photo' | translate"
          width="1999"
          height="1333"
          fetchpriority="high"
        />
      </div>
    </section>

    <!-- Section 2 — how we work -->
    <section class="sec">
      <div class="sec-head">
        <span class="eyebrow">{{ 'about.how_eyebrow' | translate }}</span>
        <h2 class="sec-title">{{ 'about.how_title' | translate }}</h2>
      </div>
      <div class="steps">
        @for (n of steps; track n) {
          <article class="step">
            <span class="step-n">{{ 'about.step' + n + '_n' | translate }}</span>
            <h3 class="step-title">{{ 'about.step' + n + '_title' | translate }}</h3>
            <p class="step-text">{{ 'about.step' + n + '_text' | translate }}</p>
          </article>
        }
      </div>
    </section>

    <!-- Section 3 — our values (sits flush under the previous section) -->
    <section class="sec sec-flush">
      <div class="sec-head">
        <span class="eyebrow">{{ 'about.values_eyebrow' | translate }}</span>
        <h2 class="sec-title">{{ 'about.values_title' | translate }}</h2>
      </div>
      <div class="values">
        @for (value of values; track value.key) {
          <div class="value">
            <span class="value-ic"><lib-icon [name]="value.icon" [size]="28" /></span>
            <h3 class="value-title">{{ 'about.value_' + value.key + '_title' | translate }}</h3>
            <p class="value-text">{{ 'about.value_' + value.key + '_text' | translate }}</p>
          </div>
        }
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      font-size: 0.82rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--gold-bright);
    }
    .eyebrow::before {
      content: '';
      inline-size: 26px;
      block-size: 2px;
      background: currentColor;
    }

    /* ----- Section 1: navy hero ------------------------------------------- */
    .hero {
      position: relative;
      overflow: hidden;
      /* Full-bleed out of .app-main's .wrap, flush under the header. */
      margin-inline: calc(50% - 50vw);
      margin-block-start: -2.5rem;
      background: linear-gradient(135deg, var(--navy) 0%, var(--navy-deep) 100%);
      color: #fff;
    }
    /* Soft radial gold glow in the top corner. */
    .hero::before {
      content: '';
      position: absolute;
      inset-block-start: -140px;
      inset-inline-end: -140px;
      inline-size: 420px;
      block-size: 420px;
      background: radial-gradient(circle, rgba(201, 151, 30, 0.32), transparent 70%);
    }
    .hero-grid {
      position: relative;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 50px;
      align-items: center;
      padding-block: clamp(48px, 7vw, 86px);
    }
    .hero-title {
      margin-block: 14px 0;
      font-size: clamp(1.7rem, 3.4vw, 2.4rem);
      font-weight: 700;
      line-height: 1.25;
      color: #fff;
    }
    .hero-body {
      margin-block: 18px 0;
      font-size: 1.06rem;
      line-height: 1.65;
      color: rgba(255, 255, 255, 0.82);
    }
    .hero-cta {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      margin-block-start: 26px;
      padding: 13px 28px;
      border-radius: 999px;
      background: var(--accent);
      color: #fff;
      font-weight: 600;
      text-decoration: none;
      transition: background 0.15s ease;
    }
    .hero-cta:hover {
      background: var(--gold-bright);
      color: #fff;
    }
    .hero-photo {
      display: block;
      inline-size: 100%;
      min-block-size: clamp(260px, 28vw, 360px);
      max-block-size: 420px;
      block-size: 100%;
      object-fit: cover;
      border-radius: var(--r-lg);
      box-shadow: var(--shadow-md);
    }
    @media (max-width: 980px) {
      .hero-grid {
        grid-template-columns: 1fr;
        gap: 28px;
      }
    }

    /* ----- Sections 2–3 ---------------------------------------------------- */
    .sec {
      padding-block: clamp(48px, 7vw, 84px);
    }
    .sec-flush {
      padding-block-start: 0;
    }
    .sec-head {
      text-align: center;
      margin-block-end: 38px;
    }
    .sec-title {
      margin-block: 12px 0;
      font-size: clamp(1.6rem, 3.4vw, 2.3rem);
      font-weight: 700;
      line-height: 1.2;
    }

    .steps {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }
    .step {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 24px;
      text-align: start;
      transition:
        transform 0.15s ease,
        box-shadow 0.15s ease;
    }
    .step:hover {
      transform: translateY(-3px);
      box-shadow: var(--shadow-sm);
    }
    .step-n {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 46px;
      block-size: 46px;
      margin-block-end: 14px;
      border-radius: 50%;
      background: var(--navy);
      color: #fff;
      font-weight: 700;
      font-size: 1.3rem;
    }
    .step-title {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 700;
    }
    .step-text {
      margin-block: 6px 0;
      font-size: 0.9rem;
      color: var(--ink-2);
    }
    @media (max-width: 980px) {
      .steps {
        grid-template-columns: repeat(3, 1fr);
      }
    }
    @media (max-width: 520px) {
      .steps {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    .values {
      display: grid;
      grid-template-columns: repeat(5, 1fr);
      gap: 18px;
    }
    .value {
      text-align: center;
    }
    .value-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 64px;
      block-size: 64px;
      margin-block-end: 14px;
      border-radius: 50%;
      background: var(--surface);
      border: 1px solid var(--line);
      box-shadow: var(--shadow-sm);
      color: var(--accent);
    }
    .value-title {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 700;
    }
    .value-text {
      margin-block: 6px 0;
      font-size: 0.88rem;
      color: var(--ink-2);
    }
    @media (max-width: 980px) {
      .values {
        grid-template-columns: repeat(2, 1fr);
      }
    }
  `,
})
export class About {
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);

  protected readonly steps = [1, 2, 3, 4] as const;
  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
    { icon: 'spark', key: 'quality' },
  ];

  // `stream` re-emits on language switch, so the SEO tags follow the active
  // language (instant() would freeze the first language's strings).
  private readonly metaTitle = toSignal(this.translate.stream('about.meta_title'));
  private readonly metaDescription = toSignal(
    this.translate.stream('about.meta_description'),
  );

  constructor() {
    effect(() => {
      const title = this.metaTitle();
      if (title) {
        this.seo.update({ title, description: this.metaDescription() });
      }
    });
  }
}
