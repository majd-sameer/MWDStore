import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';
import { ContentBlocksStore } from '../../../core/content-blocks.store';
import { AboutContentStore } from '../../../core/about-content.store';

/**
 * Mission band per supported-doc/HOME-PAGE.md §5: full-bleed navy gradient
 * with a gold corner glow. Left — eyebrow, white H2, mission paragraph and a
 * gold pill CTA to the About page. Right — a 2×2 grid of translucent value
 * cards (the first four brand values). The card copy reads the About page's
 * editable `about-values` content blocks so a single CMS edit updates both the
 * About page and this band, falling back to the `about.value_*` translations.
 * Collapses to one column under 980px.
 */
@Component({
  selector: 'app-mission-band',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './mission-band.html',
  styleUrl: './mission-band.scss',
})
export class MissionBand {
  protected readonly content = inject(ContentBlocksStore);
  protected readonly about = inject(AboutContentStore);

  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
  ];
}
