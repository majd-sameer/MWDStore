import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';

/**
 * Mission band per supported-doc/HOME-PAGE.md §5: full-bleed navy gradient
 * with a gold corner glow. Left — eyebrow, white H2, mission paragraph and a
 * gold pill CTA to the About page. Right — a 2×2 grid of translucent value
 * cards (the first four brand values, sharing the About page's `about.value_*`
 * translations). Collapses to one column under 980px.
 */
@Component({
  selector: 'app-mission-band',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  template: `
    <section class="mission">
      <div class="wrap mission-grid">
        <div>
          <span class="eyebrow">{{ 'home.mission_eyebrow' | translate }}</span>
          <h2 class="mission-title">{{ 'home.mission_title' | translate }}</h2>
          <p class="mission-body">{{ 'home.mission_body' | translate }}</p>
          <a class="mission-cta" routerLink="/pages/about-us">
            {{ 'home.mission_cta' | translate }}
            <lib-icon name="arrowEnd" [size]="18" />
          </a>
        </div>

        <div class="mission-vals">
          @for (value of values; track value.key) {
            <div class="vcard">
              <span class="vcard-ic"><lib-icon [name]="value.icon" [size]="22" /></span>
              <b>{{ 'about.value_' + value.key + '_title' | translate }}</b>
              <span>{{ 'about.value_' + value.key + '_text' | translate }}</span>
            </div>
          }
        </div>
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
      padding-block-start: clamp(48px, 7vw, 84px);
    }
    .mission {
      position: relative;
      overflow: hidden;
      margin-inline: calc(50% - 50vw);
      background: linear-gradient(135deg, var(--navy) 0%, var(--navy-deep) 100%);
      color: #fff;
    }
    .mission::before {
      content: '';
      position: absolute;
      inset-block-start: -140px;
      inset-inline-end: -140px;
      inline-size: 420px;
      block-size: 420px;
      background: radial-gradient(circle, rgba(201, 151, 30, 0.32), transparent 70%);
    }
    .mission-grid {
      position: relative;
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 50px;
      align-items: center;
      padding-block: clamp(48px, 7vw, 86px);
    }
    @media (max-width: 980px) {
      .mission-grid {
        grid-template-columns: 1fr;
        gap: 32px;
      }
    }

    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      font-size: 0.82rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      color: var(--gold-bright);
    }
    .eyebrow::before {
      content: '';
      inline-size: 26px;
      block-size: 2px;
      background: currentColor;
    }
    .mission-title {
      margin-block: 14px 0;
      font-weight: 700;
      font-size: clamp(1.6rem, 3.4vw, 2.3rem);
      line-height: 1.25;
      color: #fff;
    }
    .mission-body {
      margin-block: 18px 0;
      font-size: 1.05rem;
      line-height: 1.7;
      color: rgba(255, 255, 255, 0.82);
    }
    .mission-cta {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      margin-block-start: 28px;
      padding: 13px 28px;
      border-radius: 999px;
      background: var(--accent);
      color: #fff;
      font-weight: 600;
      text-decoration: none;
      transition: background 0.15s ease;
    }
    .mission-cta:hover {
      background: var(--gold-bright);
      color: #fff;
    }

    .mission-vals {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 14px;
    }
    @media (max-width: 520px) {
      .mission-vals {
        grid-template-columns: 1fr;
      }
    }
    .vcard {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 18px;
      background: rgba(255, 255, 255, 0.07);
      border: 1px solid rgba(255, 255, 255, 0.16);
      border-radius: var(--r);
    }
    .vcard-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 42px;
      block-size: 42px;
      margin-block-end: 4px;
      border-radius: 50%;
      background: rgba(201, 151, 30, 0.18);
      color: var(--gold-bright);
    }
    .vcard b {
      font-size: 0.98rem;
      color: #fff;
    }
    .vcard span:not(.vcard-ic) {
      font-size: 0.85rem;
      line-height: 1.55;
      color: rgba(255, 255, 255, 0.72);
    }
  `,
})
export class MissionBand {
  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
  ];
}
