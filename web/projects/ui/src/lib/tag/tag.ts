import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Small inline label chip — product tags ("New", "Low stock"), origin labels,
 * etc. Projects its content; pick a tone with `tone`.
 *
 * @example
 * <lib-tag tone="accent">New</lib-tag>
 */
@Component({
  selector: 'lib-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-tag', '[attr.data-tone]': 'tone()' },
  template: '<ng-content />',
})
export class Tag {
  readonly tone = input<'muted' | 'accent' | 'indigo' | 'success' | 'danger'>(
    'muted',
  );
}
