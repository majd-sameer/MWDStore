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
  templateUrl: './values-row.html',
  styleUrl: './values-row.scss',
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
