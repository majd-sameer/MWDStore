import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';

/**
 * Presentational form-field wrapper: renders a Bootstrap label, projects the
 * control, and shows hint / validation-error text. The control itself stays
 * the consumer's responsibility (so it works with template-driven or reactive
 * forms unchanged).
 *
 * @example
 * <lib-form-field label="Email" controlId="email" [required]="true"
 *                 [error]="form.controls.email.touched ? emailError() : null">
 *   <input id="email" type="email" class="form-control" formControlName="email" />
 * </lib-form-field>
 */
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
