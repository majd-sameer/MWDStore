import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';

/**
 * Our values per supported-doc/HOME-PAGE.md §8: centered head ("قيمنا / ما الذي
 * نؤمن به") over a 5-column row of value cards — 64px bordered white chip with
 * a gold icon, bold title and muted text. Shares the About page's
 * `about.value_*` translations (the copy is identical by spec). 2 columns
 * under 980px.
 */
@Component({
  selector: 'app-values-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Icon],
  template: `
    <section class="sec">
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
    .sec {
      padding-block: clamp(48px, 7vw, 84px) 0;
    }
    .sec-head {
      text-align: center;
      margin-block-end: 36px;
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
      margin-block: 12px 0;
      font-weight: 700;
      font-size: clamp(1.6rem, 3.4vw, 2.3rem);
      letter-spacing: -0.02em;
    }

    .values {
      display: grid;
      grid-template-columns: repeat(5, 1fr);
      gap: 18px;
    }
    @media (max-width: 980px) {
      .values {
        grid-template-columns: repeat(2, 1fr);
      }
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
  `,
})
export class ValuesRow {
  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
    { icon: 'spark', key: 'quality' },
  ];
}
