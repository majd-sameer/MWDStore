import { TestBed } from '@angular/core/testing';
import { AuthService } from 'core';
import { CartService, type CartModel } from 'data-access';
import { CartStore, type CartProduct } from './cart.store';
import type { CartWriteError } from './cart-messages';

/**
 * The guest bag lives in localStorage and never talks to `/api/cart`, so the stock rules the server
 * enforces for signed-in shoppers have to be repeated here. These pin that repetition: one line per
 * product, and never more of it than the snapshot says is in stock.
 */
function guestStore(): CartStore {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { isAuthenticated: () => false } },
      { provide: CartService, useValue: { cartResource: () => ({ value: () => null }) } },
    ],
  });
  return TestBed.runInInjectionContext(() => new CartStore());
}

function product(overrides: Partial<CartProduct> = {}): CartProduct {
  return {
    id: 1,
    name: 'Widget',
    thumbnailImageUrl: null,
    calculatedProductPrice: { price: 10, oldPrice: null, percentOfSaving: 0 },
    stockQuantity: 5,
    isAllowToOrder: true,
    stockTrackingIsEnabled: true,
    ...overrides,
  };
}

/** Runs an add and returns the cart it produced, failing the test if it errored. */
function addSync(store: CartStore, p: CartProduct, quantity: number): CartModel {
  let cart: CartModel | undefined;
  store.add(p, quantity).subscribe({ next: (value) => (cart = value) });
  if (!cart) {
    throw new Error('add did not emit');
  }
  return cart;
}

/** Runs an add expected to fail and returns the error it raised. */
function addError(store: CartStore, p: CartProduct, quantity: number): CartWriteError {
  let error: CartWriteError | undefined;
  store.add(p, quantity).subscribe({ error: (e: CartWriteError) => (error = e) });
  if (!error) {
    throw new Error('add did not error');
  }
  return error;
}

describe('CartStore guest bag — stock is a ceiling', () => {
  it('raises the existing line instead of adding a second one', () => {
    const store = guestStore();
    addSync(store, product(), 2);
    const cart = addSync(store, product(), 1);

    expect(cart.items?.length).toBe(1);
    expect(cart.items?.[0].quantity).toBe(3);
    expect(cart.adjustment).toBeNull();
  });

  it('caps the line at stock and reports the adjustment', () => {
    const store = guestStore();
    addSync(store, product({ stockQuantity: 5 }), 3);
    const cart = addSync(store, product({ stockQuantity: 5 }), 4);

    expect(cart.items?.[0].quantity).toBe(5);
    expect(cart.adjustment).toEqual({
      productId: 1,
      requestedQuantity: 4,
      appliedQuantity: 5,
      availableQuantity: 5,
    });
  });

  it('refuses once the bag holds every unit in stock', () => {
    const store = guestStore();
    addSync(store, product({ stockQuantity: 2 }), 2);

    expect(addError(store, product({ stockQuantity: 2 }), 1).code).toBe('out-of-stock');
    expect(store.items()[0].quantity).toBe(2);
  });

  it('refuses a product that is not orderable', () => {
    const store = guestStore();
    expect(addError(store, product({ isAllowToOrder: false }), 1).code).toBe('unavailable');
    expect(store.items()).toEqual([]);
  });

  it('leaves an untracked product uncapped', () => {
    const store = guestStore();
    const cart = addSync(
      store,
      product({ stockTrackingIsEnabled: false, stockQuantity: null }),
      40,
    );

    expect(cart.items?.[0].quantity).toBe(40);
    expect(cart.adjustment).toBeNull();
  });

  it('caps a quantity set through update, too', () => {
    const store = guestStore();
    addSync(store, product({ stockQuantity: 3 }), 1);

    let cart: CartModel | undefined;
    store.update(1, { quantity: 9 }).subscribe({ next: (value) => (cart = value) });

    expect(cart?.items?.[0].quantity).toBe(3);
    expect(cart?.adjustment?.appliedQuantity).toBe(3);
  });
});
