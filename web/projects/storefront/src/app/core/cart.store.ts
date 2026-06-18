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
  type CartModel,
  type UpdateCartItemRequest,
} from 'data-access';
import { concatMap, finalize, from, Observable, of, tap } from 'rxjs';

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

  /** Adds a product (or increments its line). Server-backed when signed in, local for guests. */
  add(product: CartProduct, quantity = 1): Observable<CartModel> {
    if (this.auth.isAuthenticated()) {
      return this.cartService
        .addItem({ productId: product.id, quantity })
        .pipe(tap((cart) => this.applyServerCart(cart)));
    }

    const lines = [...this.guestLines()];
    const existing = lines.findIndex((l) => l.productId === product.id);
    if (existing >= 0) {
      lines[existing] = { ...lines[existing], quantity: lines[existing].quantity + quantity };
    } else {
      lines.push(this.toGuestLine(product, quantity));
    }
    this.setGuest(lines);
    return of(this.buildGuestCart(lines));
  }

  /** Sets the quantity of a line. `id` is the cart-item id when signed in, else the product id. */
  update(id: number, request: UpdateCartItemRequest): Observable<CartModel> {
    if (this.auth.isAuthenticated()) {
      return this.cartService
        .updateItem(id, request)
        .pipe(tap((cart) => this.applyServerCart(cart)));
    }

    const quantity = request.quantity ?? 1;
    const lines = this.guestLines().map((l) =>
      l.productId === id ? { ...l, quantity } : l,
    );
    this.setGuest(lines);
    return of(this.buildGuestCart(lines));
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

  private buildGuestCart(lines: readonly GuestLine[]): CartModel {
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
    }));

    return {
      customerId: 0,
      couponCode: null,
      couponValidationErrorMessage: null,
      items,
      subTotal: items.reduce((sum, i) => sum + i.calculatedProductPrice.price * i.quantity, 0),
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
