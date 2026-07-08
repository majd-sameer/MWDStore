import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * The storefront icon vocabulary. Directional names (arrows / chevrons that
 * point "forward") are flagged so they mirror under `dir="rtl"` via the
 * `.icon-directional` class; non-directional glyphs (bag, user, truck, leaf…)
 * never flip.
 */
export type IconName =
  | 'menu'
  | 'search'
  | 'user'
  | 'bag'
  | 'plus'
  | 'minus'
  | 'arrowStart'
  | 'arrowEnd'
  | 'chevStart'
  | 'chevEnd'
  | 'chevDown'
  | 'truck'
  | 'leaf'
  | 'shield'
  | 'box'
  | 'spark'
  | 'pin'
  | 'star'
  | 'check'
  | 'x'
  | 'heart'
  | 'trash'
  | 'pencil'
  | 'eye'
  | 'grid'
  | 'filter'
  | 'hands'
  | 'award'
  | 'lock'
  | 'return'
  | 'phone'
  | 'facebook'
  | 'instagram'
  | 'twitter'
  | 'youtube'
  | 'tiktok'
  | 'whatsapp';

interface IconDef {
  readonly d: string;
  /** Points "forward" — must mirror in RTL. */
  readonly directional?: boolean;
  /** Solid glyph (filled) rather than stroked line-art. */
  readonly fill?: boolean;
}

const ICONS: Record<IconName, IconDef> = {
  menu: { d: 'M3 6h18 M3 12h18 M3 18h18' },
  search: { d: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M20 20l-3.5-3.5' },
  user: { d: 'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 21c0-4 4-6 8-6s8 2 8 6' },
  bag: { d: 'M6 7h12l1 13H5L6 7z M9 7V5.5a3 3 0 0 1 6 0V7' },
  plus: { d: 'M12 5v14 M5 12h14' },
  minus: { d: 'M5 12h14' },
  arrowStart: { d: 'M19 12H5 M11 6l-6 6 6 6', directional: true },
  arrowEnd: { d: 'M5 12h14 M13 6l6 6-6 6', directional: true },
  chevStart: { d: 'M15 6l-6 6 6 6', directional: true },
  chevEnd: { d: 'M9 6l6 6-6 6', directional: true },
  chevDown: { d: 'M6 9l6 6 6-6' },
  truck: {
    d: 'M3 7h11v9H3z M14 10h4l3 3v3h-3 M3 16h2 M9 16h2 M6.5 19a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z M17.5 19a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z',
  },
  leaf: { d: 'M5 19c0-8 6-14 14-14 0 8-6 14-14 14z M5 19c4-4 7-6 11-8' },
  shield: { d: 'M12 3l7 3v5c0 5-3 8-7 10-4-2-7-5-7-10V6l7-3z' },
  box: { d: 'M3 7l9-4 9 4-9 4-9-4z M3 7v10l9 4 9-4V7 M12 11v10' },
  spark: { d: 'M12 3l2 6 6 2-6 2-2 6-2-6-6-2 6-2z' },
  pin: { d: 'M12 21s-6-5-6-11a6 6 0 0 1 12 0c0 6-6 11-6 11z M12 8a2 2 0 1 0 0 4 2 2 0 0 0 0-4z' },
  star: {
    d: 'M12 3l2.6 6.3 6.8.5-5.2 4.4 1.6 6.6L12 17.8 6.2 21.3l1.6-6.6L2.6 9.8l6.8-.5L12 3z',
    fill: true,
  },
  check: { d: 'M5 13l4 4 10-11' },
  x: { d: 'M6 6l12 12 M18 6L6 18' },
  heart: { d: 'M12 20s-7-4.5-7-10a4 4 0 0 1 7-2.5A4 4 0 0 1 19 10c0 5.5-7 10-7 10z' },
  trash: { d: 'M4 7h16 M9 7V5h6v2 M6 7l1 13h10l1-13' },
  pencil: { d: 'M4 20l1-4L16 5l3 3L8 19l-4 1z M14 7l3 3' },
  eye: {
    d: 'M2 12s3.5-6.5 10-6.5S22 12 22 12s-3.5 6.5-10 6.5S2 12 2 12z M12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z',
  },
  grid: { d: 'M4 4h7v7H4z M13 4h7v7h-7z M4 13h7v7H4z M13 13h7v7h-7z' },
  filter: { d: 'M3 5h18 M7 12h10 M10 19h4' },
  hands: {
    d: 'M12 6c.9-1.8 3.5-1.8 4.4 0 .6 1.3 0 2.6-1.2 3.6L12 12 8.8 9.6C7.6 8.6 7 7.3 7.6 6c.9-1.8 3.5-1.8 4.4 0z M3 14.5c2 2.9 5 4.7 9 4.7s7-1.8 9-4.7',
  },
  award: { d: 'M12 3a5 5 0 1 0 0 10 5 5 0 0 0 0-10z M8.5 11.5 7 21l5-2.8L17 21l-1.5-9.5' },
  lock: { d: 'M5 11h14v10H5z M8 11V7a4 4 0 0 1 8 0v4 M12 15v3' },
  return: { d: 'M9 14L4 9l5-5 M4 9h10a6 6 0 0 1 0 12h-4', directional: true },
  phone: { d: 'M7 3H4a1 1 0 0 0-1 1c0 8.3 6.7 15 15 15a1 1 0 0 0 1-1v-3a1 1 0 0 0-.8-1l-3.5-.7a1 1 0 0 0-1 .4l-1 1.3a12 12 0 0 1-5.4-5.4l1.3-1a1 1 0 0 0 .4-1L8 3.8A1 1 0 0 0 7 3z' },
  facebook: { d: 'M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z' },
  instagram: {
    d: 'M7 2h10a5 5 0 0 1 5 5v10a5 5 0 0 1-5 5H7a5 5 0 0 1-5-5V7a5 5 0 0 1 5-5z M16 11.4A4 4 0 1 1 12.6 8 4 4 0 0 1 16 11.4z M17.5 6.5h.01',
  },
  twitter: {
    d: 'M23 3a10.9 10.9 0 0 1-3.14 1.53 4.48 4.48 0 0 0-7.86 3v1A10.66 10.66 0 0 1 3 4s-4 9 5 13a11.64 11.64 0 0 1-7 2c9 5 20 0 20-11.5a4.5 4.5 0 0 0-.08-.83A7.72 7.72 0 0 0 23 3z',
  },
  youtube: {
    d: 'M22.54 6.42a2.78 2.78 0 0 0-1.94-2C18.88 4 12 4 12 4s-6.88 0-8.6.46a2.78 2.78 0 0 0-1.94 2A29 29 0 0 0 1 12a29 29 0 0 0 .46 5.58A2.78 2.78 0 0 0 3.4 19.6c1.72.46 8.6.46 8.6.46s6.88 0 8.6-.46a2.78 2.78 0 0 0 1.94-2A29 29 0 0 0 23 12a29 29 0 0 0-.46-5.58z M9.75 15.02l5.75-3.27-5.75-3.27z',
  },
  tiktok: {
    d: 'M14 4c.4 2.6 2.4 4.4 5 4.5v3c-1.8 0-3.5-.6-5-1.6V15a5.5 5.5 0 1 1-5.5-5.5c.3 0 .6 0 .9.1v3.1a2.5 2.5 0 1 0 1.6 2.3V4z',
  },
  whatsapp: {
    d: 'M20 11.5a8 8 0 0 1-11.9 7L4 20l1.6-4A8 8 0 1 1 20 11.5z M9 8.5c0 4 2.5 6.5 6.5 6.5.5 0 1-.5 1-1.2 0-.4-1.7-1.1-2-1.1s-.6.6-.9.6C13 12.2 11.8 11 11.5 10c0-.3.6-.5.6-.9S11.4 7.5 11 7.4c-.7-.2-2 .4-2 1.1z',
  },
};

/**
 * Inline-SVG icon. Renders from the shared {@link IconName} set at the given
 * pixel size, inheriting `currentColor`. Decorative by default (`aria-hidden`);
 * pass `label` to expose it as an image to assistive tech.
 *
 * @example
 * <lib-icon name="bag" [size]="22" />
 * <lib-icon name="arrowEnd" label="Next" />
 */
@Component({
  selector: 'lib-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'lib-icon d-inline-flex' },
  templateUrl: './icon.html',
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(20);
  /** Optional accessible label; when omitted the icon is decorative. */
  readonly label = input<string | null>(null);

  protected readonly def = computed<IconDef>(() => ICONS[this.name()]);
}
