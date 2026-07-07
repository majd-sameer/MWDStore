import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from 'core';
import { Button, Icon } from 'ui';
import { ContentBlocksStore } from '../../../core/content-blocks.store';

/**
 * Home hero per supported-doc/HOME-PAGE.md §1: ivory band with a soft gold
 * glow, 2-column grid — copy (eyebrow, gold-accented H1, lead, green + ghost
 * CTAs, 3 stats) beside a 4:5 photo with a floating impact badge. The centers /
 * products stats are fed from `data-access` by the Home page; the proceeds
 * figure is brand copy. All copy keyed through ngx-translate; numerals follow
 * the active locale (Arabic-Indic in ar).
 */
@Component({
  selector: 'app-hero',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, TranslatePipe, Button, Icon],
  templateUrl: './hero.html',
  styleUrl: './hero.scss',
})
export class Hero {
  private readonly language = inject(LanguageService);
  protected readonly content = inject(ContentBlocksStore);

  /** Number of reform & rehabilitation centers (active vendor count from the API). */
  readonly centers = input<number | null>(null);
  /** Total handmade products in the catalog (from the API). */
  readonly products = input<number | null>(null);

  protected readonly locale = computed(() =>
    this.language.lang() === 'ar' ? 'ar' : 'en-US',
  );
}
