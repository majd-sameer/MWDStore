import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';


@Component({
  selector: 'lib-form-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'mb-3',
  },
  templateUrl: './form-field.html',
})
export class FormField {
  readonly label = input<string | null>(null);
  readonly hint = input<string | null>(null);
  readonly error = input<string | null>(null);
  readonly controlId = input<string | null>(null);
  readonly required = input(false, { transform: booleanAttribute });
}
