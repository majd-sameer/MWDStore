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
  templateUrl: './story-rail.html',
  styleUrl: './story-rail.scss',
})
export class StoryRail {
  readonly items = input<readonly NewsListItemDto[]>([]);

  protected initial(item: NewsListItemDto): string {
    return item.name?.trim().charAt(0) ?? '';
  }
}
