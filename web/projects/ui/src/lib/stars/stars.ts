import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';


@Component({
  selector: 'lib-stars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-stars' },
  templateUrl: './stars.html',
})
export class Stars {
  readonly rating = input<number | null>(null);
  readonly count = input<number | null>(null);
  readonly max = input(5);

  protected readonly pct = computed(() => {
    const r = this.rating() ?? 0;
    return Math.max(0, Math.min(100, (r / this.max()) * 100));
  });

  protected readonly ariaLabel = computed(() => {
    const r = this.rating();
    return r == null ? 'Not yet rated' : `${r} out of ${this.max()}`;
  });
}
