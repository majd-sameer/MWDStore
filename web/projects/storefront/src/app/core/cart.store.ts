import { isPlatformBrowser } from '@angular/common';
import {
  computed,
  effect,
  inject,
  Injectable,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { AuthService } from 'core';
import {
  CartService,
  type CalculatedProductPrice,
  type CartItemModel,
  type CartLineAdjustment,
  type CartModel,
  type UpdateCartItemRequest,
} from 'data-access';
import { concatMap, finalize, from, Observable, of, tap, throwError } from 'rxjs';
import { cartWriteError } from './cart-messages';

/** Minimal product shape needed to add to the cart and render a guest line. */
export interface CartProduct {
  id: number;
  name: string | null;
  thumbnailImageUrl: string | null;
  calculatedProductPrice: CalculatedProductPrice;
  stockQuantity: number | null;
  isAllowToOrder: boolean;
  stockTrackingIsEnabled?: boolean;
}

/** A guest cart line persisted to localStorage (a product snapshot + quantity). */
interface GuestLine {
  productId: number;
  name: string | null;
  imageUrl: string | null;
  price: number;
  oldPrice: number | null;
  stockQuantity: number | null;
  stockTrackingIsEnabled: boolean;
  isAllowToOrder: boolean;
  quantity: number;
}

const GUEST_CART_KEY = 'atb_guest_cart';

/**
 * Cart state shared by the navbar badge, the drawer, the cart page and checkout.
 *
 * Two modes, switched on authentication:
 * - **Guest** (logged out): the cart lives in `localStorage` as product
 *   snapshots, so a visitor can browse and build a bag without ever calling the
 *   protected `/api/cart` (no 401, no login bounce).
 * - **Signed in**: the server cart is the source of truth via an `httpResource`
 *   (browser-only — never fetched during SSR).
 *
 * When a guest signs in, the local cart is merged into the server cart and then
 * cleared (see the constructor effect).
 */
@Injectable({ providedIn: 'root' })
export class CartStore {
  private readonly cartService = inject(CartService);
  private readonly auth = inject(AuthService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Coupon code applied to the server cart — feeds the resource so it revalidates. */
  private readonly couponCode = signal<string | undefined>(undefined);

  /** Server cart resource — created only in the browser, fetched only when authenticated. */
  private readonly resource = this.isBrowser
    ? this.cartService.cartResource(
        () => this.couponCode(),
        () => this.auth.isAuthenticated(),
      )
    : null;

  /** Optimistic override set from server command responses. */
  private readonly override = signal<CartModel | null>(null);

  /** Guest cart lines (browser localStorage). */
  private readonly guestLines = signal<readonly GuestLine[]>(this.loadGuest());

  /** Guards the one-shot guest→server merge while it is in flight. */
  private merging = false;

  readonly cart = computed<CartModel | null>(() => {
    if (this.auth.isAuthenticated()) {
      return this.override() ?? this.resource?.value() ?? null;
    }
    return this.buildGuestCart(this.guestLines());
  });
  readonly isLoading = computed(() =>
    this.auth.isAuthenticated() ? (this.resource?.isLoading() ?? false) : false,
  );
  readonly items = computed(() => this.cart()?.items ?? []);
  readonly count = computed(() =>
    this.items().reduce((total, item) => total + item.quantity, 0),
  );
  readonly subTotal = computed(() => this.cart()?.subTotal ?? 0);
  readonly discount = computed(() => this.cart()?.discount ?? 0);
  /** The coupon echoed by the server (set only when authenticated). */
  readonly appliedCoupon = computed(() => this.cart()?.couponCode ?? null);
  /** Server-side validation message when a coupon is rejected. */
  readonly couponError = computed(
    () => this.cart()?.couponValidationErrorMessage ?? null,
  );

  constructor() {
    // Merge a guest cart into the server cart once the visitor is authenticated
    // (after login/register or a silent boot restore), then clear it locally.
    if (this.isBrowser) {
      effect(() => {
        if (this.auth.isAuthenticated() && this.guestLines().length && !this.merging) {
          this.mergeGuestCart();
        }
      });
    }
  }

  /**
   * Adds a product, raising the existing line rather than adding a second one.
   *
   * Stock is a ceiling in both modes. Signed in, the server enforces it (and it counts stock that
   * other shoppers' unpaid orders are holding, which the browser cannot know); as a guest the same
   * rule is applied here against the product snapshot so the bag never shows a quantity checkout
   * would refuse. A request that stock cuts short still succeeds and reports `cart.adjustment`;
   * one that can take nothing fails with an {@link CartWriteError}.
   */
  add(product: CartProduct, quantity = 1): Observable<CartModel> {
    if (this.auth.isAuthenticated()) {
      return this.cartService
        .addItem({ productId: product.id, quantity })
        .pipe(tap((cart) => this.applyServerCart(cart)));
    }

    const lines = [...this.guestLines()];
    const existing = lines.findIndex((l) => l.productId === product.id);
    const current = existing >= 0 ? lines[existing].quantity : 0;
    const cap = this.guestCap(product.stockTrackingIsEnabled ?? false, product.stockQuantity);

    if (!product.isAllowToOrder) {
      return throwError(() => cartWriteError('unavailable', cap));
    }
    if (cap !== null && current >= cap) {
      return throwError(() => cartWriteError('out-of-stock', cap));
    }

    const target = cap === null ? current + quantity : Math.min(current + quantity, cap);
    if (existing >= 0) {
      lines[existing] = { ...lines[existing], quantity: target };
    } else {
      lines.push(this.toGuestLine(product, target));
    }
    this.setGuest(lines);

    const adjustment: CartLineAdjustment | null =
      target < current + quantity
        ? {
            productId: product.id,
            requestedQuantity: quantity,
            appliedQuantity: target,
            availableQuantity: cap ?? target,
          }
        : null;
    return of(this.buildGuestCart(lines, adjustment));
  }

  /**
   * Sets the quantity of a line, capped by stock the same way {@link add} is. `id` is the cart-item
   * id when signed in, else the product id.
   */
  update(id: number, request: UpdateCartItemRequest): Observable<CartModel> {
    if (this.auth.isAuthenticated()) {
      return this.cartService
        .updateItem(id, request)
        .pipe(tap((cart) => this.applyServerCart(cart)));
    }

    const requested = request.quantity ?? 1;
    const line = this.guestLines().find((l) => l.productId === id);
    const cap = line ? this.guestCap(line.stockTrackingIsEnabled, line.stockQuantity) : null;
    if (cap === 0) {
      return throwError(() => cartWriteError('out-of-stock', 0));
    }

    const target = cap === null ? requested : Math.min(requested, cap);
    const lines = this.guestLines().map((l) =>
      l.productId === id ? { ...l, quantity: target } : l,
    );
    this.setGuest(lines);

    const adjustment: CartLineAdjustment | null =
      target < requested
        ? {
            productId: id,
            requestedQuantity: requested,
            appliedQuantity: target,
            availableQuantity: cap ?? target,
          }
        : null;
    return of(this.buildGuestCart(lines, adjustment));
  }

  /** The stock ceiling for a guest line, or null when the product is not stock-tracked. */
  private guestCap(tracksStock: boolean, stockQuantity: number | null): number | null {
    return tracksStock ? Math.max(0, stockQuantity ?? 0) : null;
  }

  /** Removes a line. `id` is the cart-item id when signed in, else the product id. */
  remove(id: number): Observable<unknown> {
    if (this.auth.isAuthenticated()) {
      return this.cartService.removeItem(id).pipe(tap(() => this.reload()));
    }

    this.setGuest(this.guestLines().filter((l) => l.productId !== id));
    return of(void 0);
  }

  /** Re-reads the cart from the server (e.g. after login changes the user). */
  reload(): void {
    this.override.set(null);
    this.resource?.reload();
  }

  /** Empties the guest cart (localStorage + memory) — call after a guest order is placed. */
  clearGuest(): void {
    this.setGuest([]);
  }

  /** Applies a coupon to the server cart; the resource revalidates it and reports the discount/error. */
  applyCoupon(code: string): void {
    this.couponCode.set(code.trim() || undefined);
    this.override.set(null);
    this.resource?.reload();
  }

  /** Removes any applied coupon and revalidates the cart at full price. */
  clearCoupon(): void {
    this.couponCode.set(undefined);
    this.override.set(null);
    this.resource?.reload();
  }

  /**
   * Stores a command's cart response. When a coupon is active the command echo
   * omits the discount, so refetch through the resource (which re-applies the
   * coupon) instead of trusting the optimistic echo.
   */
  private applyServerCart(cart: CartModel): void {
    if (this.couponCode()) {
      this.reload();
    } else {
      this.override.set(cart);
    }
  }

  // --- guest → server merge -------------------------------------------------

  private mergeGuestCart(): void {
    this.merging = true;
    const lines = [...this.guestLines()];
    from(lines)
      .pipe(
        concatMap((line) =>
          this.cartService.addItem({ productId: line.productId, quantity: line.quantity }),
        ),
        finalize(() => {
          this.setGuest([]);
          this.reload();
          this.merging = false;
        }),
      )
      .subscribe({ error: () => undefined });
  }

  // --- guest cart helpers ---------------------------------------------------

  private toGuestLine(product: CartProduct, quantity: number): GuestLine {
    return {
      productId: product.id,
      name: product.name,
      imageUrl: product.thumbnailImageUrl,
      price: product.calculatedProductPrice.price,
      oldPrice: product.calculatedProductPrice.oldPrice,
      stockQuantity: product.stockQuantity,
      stockTrackingIsEnabled: product.stockTrackingIsEnabled ?? false,
      isAllowToOrder: product.isAllowToOrder,
      quantity,
    };
  }

  private buildGuestCart(
    lines: readonly GuestLine[],
    adjustment: CartLineAdjustment | null = null,
  ): CartModel {
    const items: CartItemModel[] = lines.map((l) => ({
      // No server id for guest lines — use the product id so the cart/drawer
      // UIs (which call update/remove with item.id) round-trip correctly.
      id: l.productId,
      productId: l.productId,
      productName: l.name,
      productImageUrl: l.imageUrl,
      productPrice: l.price,
      calculatedProductPrice: { price: l.price, oldPrice: l.oldPrice, percentOfSaving: 0 },
      quantity: l.quantity,
      productStockQuantity: l.stockQuantity ?? 99,
      productStockTrackingIsEnabled: l.stockTrackingIsEnabled,
      isProductAvailableToOrder: l.isAllowToOrder,
      // Mirrors the server's rule so both carts read the same way: unavailable lines are shown but
      // never priced.
      isAvailable:
        l.isAllowToOrder &&
        (!l.stockTrackingIsEnabled || (l.stockQuantity ?? 0) >= l.quantity),
      availableQuantity: l.stockTrackingIsEnabled
        ? Math.max(0, l.stockQuantity ?? 0)
        : l.quantity,
    }));

    return {
      customerId: 0,
      couponCode: null,
      couponValidationErrorMessage: null,
      items,
      adjustment,
      subTotal: items
        .filter((i) => i.isAvailable)
        .reduce((sum, i) => sum + i.calculatedProductPrice.price * i.quantity, 0),
      discount: 0,
    };
  }

  private setGuest(lines: readonly GuestLine[]): void {
    this.guestLines.set(lines);
    if (!this.isBrowser) {
      return;
    }
    try {
      localStorage.setItem(GUEST_CART_KEY, JSON.stringify(lines));
    } catch {
      // Storage unavailable / full — keep the in-memory copy.
    }
  }

  private loadGuest(): readonly GuestLine[] {
    if (!this.isBrowser) {
      return [];
    }
    try {
      const raw = localStorage.getItem(GUEST_CART_KEY);
      const parsed = raw ? (JSON.parse(raw) as GuestLine[]) : [];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
}
