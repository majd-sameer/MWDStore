import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, effect, inject, signal } from '@angular/core';
import type { CartModel } from 'data-access';
import { cartAdjustmentMessage, type CartMessage } from './cart-messages';

/**
 * UI-only state for the slide-in cart drawer. Kept separate from CartStore
 * (which owns cart *data*) so the header can open the drawer and the shell can
 * render it without coupling either to cart contents.
 *
 * The drawer doubles as the add-to-bag confirmation: opening it right after an
 * add shows the shopper what went in, the running subtotal and the two next
 * steps (checkout / keep shopping) — persistent feedback that a vanishing toast
 * over the header could not give.
 */
@Injectable({ providedIn: 'root' })
export class CartDrawerService {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly _open = signal(false);
  private readonly _notice = signal<CartMessage | null>(null);

  readonly open = this._open.asReadonly();
  /** The "just added" banner shown at the top of the drawer, if any. */
  readonly notice = this._notice.asReadonly();

  constructor() {
    // Lock page scroll while the drawer is up so the wheel / finger scrolls the
    // bag, not the page behind it.
    effect(() => {
      if (this.isBrowser) {
        this.document.body.classList.toggle('drawer-open', this._open());
      }
    });
  }

  show(): void {
    this._notice.set(null);
    this._open.set(true);
  }

  /** Opens the drawer as confirmation of a successful add (or a stock-capped one). */
  showAdded(cart: CartModel): void {
    this._notice.set(cartAdjustmentMessage(cart) ?? { key: 'cart.added_title' });
    this._open.set(true);
  }

  close(): void {
    this._open.set(false);
    this._notice.set(null);
  }

  toggle(): void {
    if (this._open()) {
      this.close();
    } else {
      this.show();
    }
  }
}
