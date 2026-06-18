import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Read-only star rating. Renders a muted base row with an accent overlay
 * clipped to the fractional rating, plus an optional review count. The visible
 * stars are aria-hidden; the rating is announced through an accessible label.
 *
 * @example
 * <lib-stars [rating]="product().ratingAverage" [count]="product().reviewsCount" />
 */
@Component({
  selector: 'lib-stars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-stars' },
  template: `
    <span
      class="ui-stars__track"
      role="img"
      [attr.aria-label]="ariaLabel()"
    >
      <span class="ui-stars__base" aria-hidden="true">★★★★★</span>
      <span class="ui-stars__fill" aria-hidden="true" [style.inline-size.%]="pct()"
        >★★★★★</span
      >
    </span>
    @if (count() !== null) {
      <span class="ui-stars__count text-body-secondary">({{ count() }})</span>
    }
  `,
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
