import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  signal,
} from '@angular/core';
import { Icon, type IconName } from '../icon/icon';

/** Gradient tones available as the art fallback (mirror `dt-*` in tokens). */
export type TileTone =
  | 'sand'
  | 'sage'
  | 'sky'
  | 'blush'
  | 'stone'
  | 'clay'
  | 'indigo';

const TONES: readonly TileTone[] = [
  'sand',
  'sage',
  'sky',
  'blush',
  'stone',
  'clay',
  'indigo',
];

/**
 * Product / category art. Renders the API image when `src` is present; when it
 * is missing or fails to load it falls back to a brand gradient tile with a
 * line-art glyph (or an initial). The fallback tone is derived deterministically
 * from `seed` so the same product always gets the same color.
 *
 * @example
 * <lib-tile [src]="product().imageUrl" [alt]="product().name" glyph="leaf"
 *           [seed]="product().id" ratio="1x1" />
 */
@Component({
  selector: 'lib-tile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  host: { class: 'd-block' },
  templateUrl: './tile.html',
})
export class Tile {
  readonly src = input<string | null>(null);
  readonly alt = input<string | null>(null);
  readonly glyph = input<IconName | null>(null);
  /** Used to derive a stable fallback tone + the initial glyph. */
  readonly seed = input<string | number | null>(null);
  readonly tone = input<TileTone | null>(null);
  readonly ratio = input<'1x1' | '4x3' | '16x9' | '21x9'>('1x1');

  protected readonly failed = signal(false);

  protected readonly ratioClass = computed(() => `ratio-${this.ratio()}`);

  protected readonly toneClass = computed(() => {
    const explicit = this.tone();
    if (explicit) {
      return `dt-${explicit}`;
    }
    const seed = this.seed();
    const n =
      typeof seed === 'number'
        ? Math.abs(Math.trunc(seed))
        : String(seed ?? '').split('').reduce((a, c) => a + c.charCodeAt(0), 0);
    return `dt-${TONES[n % TONES.length]}`;
  });

  protected readonly initial = computed(() => {
    if (this.glyph()) {
      return null;
    }
    const seed = this.seed();
    const text = typeof seed === 'string' ? seed.trim() : '';
    return text ? text.charAt(0).toUpperCase() : null;
  });
}
