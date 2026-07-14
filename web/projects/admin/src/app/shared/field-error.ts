import type { FieldState } from '@angular/forms/signals';


export function firstError<T>(state: FieldState<T>): string | null {
  if (!state.touched()) {
    return null;
  }
  const errors = state.errors();
  return errors.length ? (errors[0].message ?? 'Invalid value') : null;
}
