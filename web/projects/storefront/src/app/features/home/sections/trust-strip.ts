import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';

interface TrustItem {
  readonly icon: IconName;
  readonly key: string;
}

/**
 * Trust strip per supported-doc/HOME-PAGE.md §2: full-bleed white band with
 * top/bottom hairlines, four reassurance items (verified products / fast
 * delivery / easy returns / secure payment), each an ivory icon chip with a
 * navy icon plus bold title and muted subtext, divided vertically. 2 columns
 * under 760px. All copy keyed.
 */
@Component({
  selector: 'app-trust-strip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Icon],
  template: `
    <div class="trust">
      <div class="wrap trust-inner">
        @for (item of items; track item.key) {
          <div class="trust-cell">
            <span class="trust-icon"><lib-icon [name]="item.icon" [size]="22" /></span>
            <div>
              <div class="trust-h">{{ 'home.' + item.key + '_h' | translate }}</div>
              <div class="trust-s">{{ 'home.' + item.key + '_s' | translate }}</div>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .trust {
      margin-inline: calc(50% - 50vw);
      background: var(--surface);
      border-block: 1px solid var(--line);
    }
    .trust-inner {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 1.5rem;
      padding-block: 26px;
    }
    .trust-cell {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .trust-cell + .trust-cell {
      border-inline-start: 1px solid var(--line-2);
      padding-inline-start: 1.5rem;
    }
    @media (max-width: 760px) {
      .trust-inner {
        grid-template-columns: repeat(2, 1fr);
        gap: 1.25rem;
      }
      .trust-cell + .trust-cell {
        border-inline-start: 0;
        padding-inline-start: 0;
      }
    }
    .trust-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: 0 0 auto;
      inline-size: 46px;
      block-size: 46px;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--navy);
    }
    .trust-h {
      font-weight: 600;
      color: var(--ink);
    }
    .trust-s {
      font-size: 0.88rem;
      color: var(--ink-3);
    }
  `,
})
export class TrustStrip {
  protected readonly items: readonly TrustItem[] = [
    { icon: 'shield', key: 'trust1' },
    { icon: 'truck', key: 'trust2' },
    { icon: 'return', key: 'trust3' },
    { icon: 'lock', key: 'trust4' },
  ];
}
