import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
  model,
} from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * Single collapsible panel (title + projected body). Stack several to form an
 * accordion group — e.g. product Description / Specification. Open state is
 * two-way bound through `open`. The chevron is non-directional (rotates), so it
 * reads correctly in both LTR and RTL.
 *
 * @example
 * <lib-accordion [title]="'product.description' | translate" [open]="true">
 *   <p>{{ product().description }}</p>
 * </lib-accordion>
 */
@Component({
  selector: 'lib-accordion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  host: { class: 'ui-accordion' },
  template: `
    <button
      type="button"
      class="ui-accordion__head"
      [attr.aria-expanded]="open()"
      (click)="toggle()"
    >
      <span class="ui-accordion__title">{{ title() }}</span>
      <lib-icon
        name="chevDown"
        [size]="18"
        class="ui-accordion__chev"
        [class.is-open]="open()"
      />
    </button>
    @if (open()) {
      <div class="ui-accordion__body">
        <ng-content />
      </div>
    }
  `,
})
export class Accordion {
  readonly title = input<string | null>(null);
  readonly open = model(false);
  readonly disabled = input(false, { transform: booleanAttribute });

  protected toggle(): void {
    if (!this.disabled()) {
      this.open.update((v) => !v);
    }
  }
}
