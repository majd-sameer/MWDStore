import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
  model,
} from '@angular/core';
import { Icon } from '../icon/icon';


@Component({
  selector: 'lib-accordion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  host: { class: 'ui-accordion' },
  templateUrl: './accordion.html',
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
