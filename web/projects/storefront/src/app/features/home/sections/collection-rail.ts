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
  template: `
    <section class="sec">
      <div class="sec-head">
        <div>
          <span class="eyebrow">{{ 'home.cats_eyebrow' | translate }}</span>
          <h2 class="sec-title">{{ 'home.cats_title' | translate }}</h2>
        </div>
        <a class="sec-link" routerLink="/shop">
          {{ 'home.cats_all' | translate }}
          <lib-icon name="arrowEnd" [size]="16" />
        </a>
      </div>

      <div class="cattiles">
        @for (cat of visible(); track cat.id; let i = $index) {
          <a
            class="cattile"
            [routerLink]="['/shop']"
            [queryParams]="{ category: cat.slug }"
          >
            <span class="cattile-ic"><lib-icon [name]="glyphFor(i)" [size]="26" /></span>
            <b class="cattile-name">{{ cat.slug | categoryLabel: cat.name }}</b>
            @if (countLabel(cat.id); as count) {
              <span class="cattile-count tabular-nums">
                {{ 'home.cats_count' | translate: { count } }}
              </span>
            }
          </a>
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
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      margin-block-end: 34px;
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
      margin-block: 10px 0;
      font-weight: 700;
      font-size: clamp(1.6rem, 3.4vw, 2.3rem);
      letter-spacing: -0.02em;
    }
    .sec-link {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      color: var(--navy);
      font-weight: 700;
      text-decoration: none;
      white-space: nowrap;
    }
    .sec-link:hover {
      color: var(--accent);
    }

    .cattiles {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: 16px;
    }
    @media (max-width: 980px) {
      .cattiles {
        grid-template-columns: repeat(3, 1fr);
      }
    }
    @media (max-width: 520px) {
      .cattiles {
        grid-template-columns: repeat(2, 1fr);
      }
    }
    .cattile {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      padding: 22px 14px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      text-align: center;
      text-decoration: none;
      color: var(--ink);
      transition:
        transform 0.15s ease,
        box-shadow 0.15s ease;
    }
    .cattile:hover {
      transform: translateY(-3px);
      box-shadow: var(--shadow-sm);
    }
    .cattile-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 58px;
      block-size: 58px;
      margin-block-end: 6px;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--accent);
    }
    .cattile-name {
      font-size: 0.95rem;
    }
    .cattile-count {
      font-size: 0.8rem;
      color: var(--ink-3);
    }
  `,
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
