import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';

/**
 * Eyebrow pill — the rounded, bordered badge used above section headlines and
 * in the hero ("This week — …"). Shows an optional leading accent dot.
 *
 * @example
 * <lib-pill [dot]="true">This week — Sicilian harvest has landed</lib-pill>
 */
@Component({
  selector: 'lib-pill',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-pill' },
  templateUrl: './pill.html',
})
export class Pill {
  readonly dot = input(false, { transform: booleanAttribute });
}
