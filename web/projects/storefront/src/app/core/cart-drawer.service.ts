import { Injectable, signal } from '@angular/core';

/**
 * UI-only state for the slide-in cart drawer. Kept separate from CartStore
 * (which owns cart *data*) so the header can open the drawer and the shell can
 * render it without coupling either to cart contents.
 */
@Injectable({ providedIn: 'root' })
export class CartDrawerService {
  private readonly _open = signal(false);
  readonly open = this._open.asReadonly();

  show(): void {
    this._open.set(true);
  }

  close(): void {
    this._open.set(false);
  }

  toggle(): void {
    this._open.update((v) => !v);
  }
}
