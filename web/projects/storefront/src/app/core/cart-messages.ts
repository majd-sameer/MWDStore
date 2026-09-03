import { HttpErrorResponse } from '@angular/common/http';
import type { TranslateService } from '@ngx-translate/core';
import type { CartModel } from 'data-access';
import type { ToastService } from 'ui';

/**
 * Why a cart write could not be applied at all.
 *
 * The signed-in path gets these from the API (a 400/404 carrying `code`); the guest path raises the
 * same shape locally so both modes report a full bag the same way.
 */
export interface CartWriteError {
  readonly isCartWriteError: true;
  /** Server error code — see `CartLineResult.ErrorCode`. */
  readonly code:
    | 'out-of-stock'
    | 'unavailable'
    | 'product-not-found'
    | 'wrong-quantity'
    | 'not-found'
    | 'error';
  /** How many are actually available, when the server said. */
  readonly available: number | null;
}

/** An i18n key plus its interpolation values, ready for `translate.instant`. */
export interface CartMessage {
  readonly key: string;
  readonly params?: Record<string, unknown>;
}

export function cartWriteError(
  code: CartWriteError['code'],
  available: number | null = null,
): CartWriteError {
  return { isCartWriteError: true, code, available };
}

/**
 * The message for a cart write that failed outright. Anything we don't recognise falls back to the
 * generic error, so an unexpected 500 never renders as a stock problem.
 */
export function cartErrorMessage(error: unknown): CartMessage {
  const parsed = parse(error);
  switch (parsed.code) {
    case 'out-of-stock':
      return parsed.available && parsed.available > 0
        ? { key: 'cart.err_stock_all_in_bag', params: { count: parsed.available } }
        : { key: 'cart.err_out_of_stock' };
    case 'unavailable':
    case 'product-not-found':
      return { key: 'cart.err_unavailable' };
    default:
      return { key: 'common.error' };
  }
}

/**
 * The message for a write that *succeeded* but was capped by stock — say so rather than letting the
 * shopper discover a smaller number than they asked for on their own.
 */
export function cartAdjustmentMessage(cart: CartModel): CartMessage | null {
  const adjustment = cart.adjustment;
  return adjustment
    ? { key: 'cart.capped_to_stock', params: { count: adjustment.appliedQuantity } }
    : null;
}

function parse(error: unknown): CartWriteError {
  if (isCartWriteError(error)) {
    return error;
  }
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { code?: string; available?: number | null } | null;
    const code = body?.code;
    if (
      code === 'out-of-stock' ||
      code === 'unavailable' ||
      code === 'product-not-found' ||
      code === 'wrong-quantity' ||
      code === 'not-found'
    ) {
      return cartWriteError(code, body?.available ?? null);
    }
  }
  return cartWriteError('error');
}

function isCartWriteError(error: unknown): error is CartWriteError {
  return typeof error === 'object' && error !== null && 'isCartWriteError' in error;
}

/** Announces an add that could not happen at all — a sold-out or withdrawn product. */
export function announceCartError(
  toast: ToastService,
  translate: TranslateService,
  error: unknown,
): void {
  const message = cartErrorMessage(error);
  toast.error(translate.instant(message.key, message.params));
}
