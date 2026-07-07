import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core';
import { formatNumber } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from 'core';
import type { CategoryDto } from 'data-access';
import { Icon, type IconName } from 'ui';
import { CategoryLabelPipe } from '../../../shared/category-label.pipe';

/**
 * Categories section per supported-doc/HOME-PAGE.md §3: gold-kicker section
 * head with an "all categories" link, then six centered white tiles — a round
 * ivory chip with a gold icon, the category name and its product count. The
 * categories come from `data-access`; counts come from the catalog search's
 * `filterOption.categories` facet (passed in by the Home page). 3 columns
 * under 980px, 2 under 520px.
 */
@Component({
  selector: 'app-collection-rail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, CategoryLabelPipe],
  templateUrl: './collection-rail.html',
  styleUrl: './collection-rail.scss',
})
export class CollectionRail {
  private readonly language = inject(LanguageService);

  readonly categories = input<readonly CategoryDto[]>([]);
  /** Product count per category id — from the catalog search facet. */
  readonly counts = input<Readonly<Record<number, number>>>({});

  protected readonly locale = computed(() =>
    this.language.lang() === 'ar' ? 'ar' : 'en-US',
  );

  protected readonly visible = computed(() => {
    const all = this.categories();
    const inMenu = all.filter((c) => c.includeInMenu);
    return (inMenu.length ? inMenu : all).slice(0, 6);
  });

  /** Localized count for a category (Arabic-Indic digits in ar), or null while unknown. */
  protected countLabel(id: number): string | null {
    const count = this.counts()[id];
    return count === undefined ? null : formatNumber(count, this.locale());
  }

  // The API has no per-category icon, so tiles rotate through the craft glyphs.
  private static readonly glyphs: readonly IconName[] = [
    'award',
    'spark',
    'leaf',
    'box',
    'shield',
    'pencil',
  ];

  protected glyphFor(index: number): IconName {
    return CollectionRail.glyphs[index % CollectionRail.glyphs.length];
  }
}
