import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';


@Component({
  selector: 'lib-pill',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-pill' },
  templateUrl: './pill.html',
})
export class Pill {
  readonly dot = input(false, { transform: booleanAttribute });
}
