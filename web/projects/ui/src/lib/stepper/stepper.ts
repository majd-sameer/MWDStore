import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  model,
} from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * Quantity stepper (−/value/+). Two-way bound through `value`; clamps to
 * `min`/`max`. Uses logical layout so the −/+ order mirrors in RTL.
 *
 * @example
 * <lib-stepper [(value)]="qty" [min]="1" [max]="stock()" />
 */
@Component({
  selector: 'lib-stepper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  host: { class: 'ui-stepper' },
  templateUrl: './stepper.html',
})
export class Stepper {
  readonly value = model(1);
  readonly min = input(1);
  readonly max = input(Number.MAX_SAFE_INTEGER);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly decrementLabel = input('Decrease quantity');
  readonly incrementLabel = input('Increase quantity');

  protected readonly range = computed(() => ({
    min: this.min(),
    max: this.max(),
  }));

  protected step(delta: number): void {
    const { min, max } = this.range();
    const next = Math.min(max, Math.max(min, this.value() + delta));
    if (next !== this.value()) {
      this.value.set(next);
    }
  }
}
