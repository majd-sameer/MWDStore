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
  template: `
    @if (label()) {
      <label class="form-label" [attr.for]="controlId()">
        {{ label() }}
        @if (required()) {
          <span class="text-danger" aria-hidden="true">*</span>
        }
      </label>
    }
    <ng-content />
    @if (error()) {
      <div class="invalid-feedback d-block">{{ error() }}</div>
    } @else if (hint()) {
      <div class="form-text">{{ hint() }}</div>
    }
  `,
})
export class FormField {
  readonly label = input<string | null>(null);
  readonly hint = input<string | null>(null);
  readonly error = input<string | null>(null);
  readonly controlId = input<string | null>(null);
  readonly required = input(false, { transform: booleanAttribute });
}
