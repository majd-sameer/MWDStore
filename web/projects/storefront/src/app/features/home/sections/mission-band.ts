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
  templateUrl: './mission-band.html',
  styleUrl: './mission-band.scss',
})
export class MissionBand {
  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
  ];
}
