import type { FieldState } from '@angular/forms/signals';

/**
 * Returns the first validation message for a Signal Forms field, but only once
 * the field has been touched — so errors appear after interaction, not on load.
 * Call inside a `computed` (or directly in a template) so it re-evaluates as the
 * field's state changes.
 */
export function firstError<T>(state: FieldState<T>): string | null {
  if (!state.touched()) {
    return null;
  }
  const errors = state.errors();
  return errors.length ? (errors[0].message ?? 'Invalid value') : null;
}
