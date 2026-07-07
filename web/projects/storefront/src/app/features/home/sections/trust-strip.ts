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
  templateUrl: './trust-strip.html',
  styleUrl: './trust-strip.scss',
})
export class TrustStrip {
  protected readonly items: readonly TrustItem[] = [
    { icon: 'shield', key: 'trust1' },
    { icon: 'truck', key: 'trust2' },
    { icon: 'return', key: 'trust3' },
    { icon: 'lock', key: 'trust4' },
  ];
}
