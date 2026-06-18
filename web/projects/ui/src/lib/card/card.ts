import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Bootstrap card wrapper. Body content is projected; an optional `header` and
 * `footer` string render the matching Bootstrap cap sections.
 *
 * @example
 * <lib-card header="Order #1024" footer="Updated just now">
 *   <p class="mb-0">3 items · $129.00</p>
 * </lib-card>
 */
@Component({
  selector: 'lib-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'card',
  },
  template: `
    @if (header()) {
      <div class="card-header">{{ header() }}</div>
    }
    <div class="card-body">
      @if (title()) {
        <h5 class="card-title">{{ title() }}</h5>
      }
      <ng-content />
    </div>
    @if (footer()) {
      <div class="card-footer text-body-secondary">{{ footer() }}</div>
    }
  `,
})
export class Card {
  readonly header = input<string | null>(null);
  readonly title = input<string | null>(null);
  readonly footer = input<string | null>(null);
}
