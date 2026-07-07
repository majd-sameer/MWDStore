import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { form, FormField as Control, required } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, MoneyPipe } from 'core';
import {
  AccountService,
  type AddressDto,
  CheckoutService,
  type GuestCartLine,
  type GuestPlaceOrderRequest,
  LocationsService,
  type OrderDetailDto,
  OrderService,
  type PaymentMethodDto,
  PaymentsService,
  type PlaceOrderRequest,
  type ShippingOptionDto,
  type StateOrProvinceLookupDto,
} from 'data-access';
import { Button, Icon, type IconName, Tile, ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';

type Stage = 'cart' | 'checkout' | 'done';

/** Icon shown for each gateway in the payment-method list, keyed by provider id. */
const PAY_ICONS: Record<string, IconName> = {
  CoD: 'truck',
  Stripe: 'lock',
  Braintree: 'lock',
  PaypalExpress: 'lock',
  MEPS: 'phone',
};

/**
 * i18n keys for the known providers, keyed by provider id. Unknown providers fall
 * back to the backend-supplied name (passing the raw name through `translate`
 * returns it unchanged when there's no matching key).
 */
const PAY_LABEL_KEYS: Record<string, string> = {
  CoD: 'checkout.methods.cod',
  Stripe: 'checkout.methods.stripe',
  Braintree: 'checkout.methods.braintree',
  PaypalExpress: 'checkout.methods.paypal',
  MEPS: 'checkout.methods.meps',
};

/**
 * i18n keys for the known shipping carriers, keyed by provider id. Unknown carriers fall back
 * to the backend-supplied name (passing it through `translate` returns it unchanged).
 */
const SHIP_LABEL_KEYS: Record<string, string> = {
  Aramex: 'checkout.carriers.aramex',
  JordanPost: 'checkout.carriers.jordan_post',
  Free: 'checkout.carriers.free',
  TableRate: 'checkout.carriers.standard',
};

interface AddressModel {
  contactName: string;
  phone: string;
  area: string;
  addressDetail: string;
  // Kept as strings so [formField] binds to the <select>; converted on submit.
  stateOrProvinceId: string;
  countryId: string;
}

/**
 * Cart → Checkout → Confirmation in one screen (per supported-doc/CART-PAGE.md):
 * a step indicator over a 2-column grid (items/forms + sticky order summary)
 * that advances through an internal `stage` state machine. Wired to the real
 * backend — the shared CartStore for lines, CheckoutService for shipping options
 * and place-order, LocationsService for the governorate list. Guests build a bag
 * freely and are gated to sign in only when they proceed to pay. Copy is keyed
 * through ngx-translate; layout uses logical properties so it mirrors in RTL.
 */
@Component({
  selector: 'app-checkout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Control, Button, Icon, Tile],
  templateUrl: './checkout.html',
  styleUrl: './checkout.scss',
})
export class Checkout {
  protected readonly cart = inject(CartStore);
  protected readonly auth = inject(AuthService);
  private readonly checkout = inject(CheckoutService);
  private readonly payments = inject(PaymentsService);
  private readonly account = inject(AccountService);
  private readonly orderService = inject(OrderService);
  private readonly locations = inject(LocationsService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly stage = signal<Stage>('cart');
  /** Selected payment method = the chosen provider id (e.g. `CoD`, `MEPS`). */
  protected readonly pay = signal<string>('');
  protected readonly promo = signal('');
  protected readonly email = signal('');
  protected readonly orderNote = signal('');
  protected readonly busy = signal(false);
  protected readonly placing = signal(false);
  protected readonly placedOrder = signal<OrderDetailDto | null>(null);

  /** Enabled payment methods, sourced from the admin payment-provider config. */
  protected readonly paymentMethods = this.payments.methodsResource();
  protected readonly payMethods = computed(() => this.paymentMethods.value() ?? []);

  protected payIcon(id: string): IconName {
    return PAY_ICONS[id] ?? 'lock';
  }

  /** Localized label for a method (i18n key for known providers, backend name otherwise). */
  protected payLabel(m: PaymentMethodDto): string {
    return PAY_LABEL_KEYS[m.id] ?? m.name ?? m.id;
  }

  /** Localized label for a shipping carrier (i18n key for known carriers, backend name otherwise). */
  protected shipLabel(o: ShippingOptionDto): string {
    return SHIP_LABEL_KEYS[o.id ?? ''] ?? o.name ?? o.id ?? '';
  }

  /** Pick a shipping carrier from the dropdown (matched by provider id). */
  protected onShippingChange(id: string): void {
    this.selectedShipping.set(this.shippingOptions().find((o) => o.id === id) ?? null);
  }

  /** Localized label of the selected method (for the confirmation screen). */
  protected readonly selectedMethodLabel = computed(() => {
    const m = this.payMethods().find((x) => x.id === this.pay());
    return m ? this.payLabel(m) : '';
  });

  protected readonly model = signal<AddressModel>({
    contactName: '',
    phone: '',
    area: '',
    addressDetail: '',
    stateOrProvinceId: '',
    countryId: '',
  });

  protected readonly f = form(this.model, (path) => {
    required(path.contactName);
    required(path.phone);
    required(path.area);
    required(path.stateOrProvinceId);
  });

  private readonly countries = this.locations.countriesResource();
  protected readonly states = signal<StateOrProvinceLookupDto[]>([]);

  // Prefill sources for signed-in customers: the profile (name/phone/email) and
  // the most recent order's shipping address (the only saved-address we have).
  // Both stay idle for guests so the cart stage makes no authed calls.
  private readonly authed = computed(() => this.auth.isAuthenticated());
  private readonly profile = this.account.profileResource(this.authed);
  private readonly orders = this.orderService.ordersResource(this.authed);
  private readonly newestOrderId = computed(() => {
    const list = this.orders.value();
    if (!list || list.length === 0) {
      return 0;
    }
    return [...list].sort(
      (a, b) => Date.parse(b.createdOn) - Date.parse(a.createdOn),
    )[0].id;
  });
  private readonly lastOrder = this.orderService.orderResource(this.newestOrderId);
  protected readonly shippingOptions = signal<ShippingOptionDto[]>([]);
  protected readonly selectedShipping = signal<ShippingOptionDto | null>(null);

  /** All required address fields filled (used to fetch shipping). Address detail is optional. */
  protected readonly addressValid = computed(
    () =>
      !this.f.contactName().invalid() &&
      !this.f.phone().invalid() &&
      !this.f.area().invalid() &&
      !this.f.stateOrProvinceId().invalid() &&
      !!this.model().countryId,
  );

  /**
   * Email is optional. When provided it's the guest's order-tracking secret, so we still flag a
   * malformed address; an empty email is allowed and simply means the order can't be tracked by email.
   */
  protected readonly emailValid = computed(() => {
    const value = this.email().trim();
    return value.length === 0 || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
  });

  /** Guest cart lines as a place-order payload (guests have no server cart). */
  private readonly guestItems = computed<GuestCartLine[]>(() =>
    this.cart.items().map((i) => ({ productId: i.productId, quantity: i.quantity })),
  );

  protected readonly canPlaceOrder = computed(
    () =>
      this.addressValid() &&
      !!this.selectedShipping() &&
      !!this.pay() &&
      (this.auth.isAuthenticated() || this.emailValid()),
  );

  // One-time prefill guards (contact from profile, address from last order).
  private contactSeeded = false;
  private addressSeeded = false;

  constructor() {
    // Prefill contact details from the signed-in customer's profile. Only fills
    // empty fields, so anything the shopper has typed wins.
    effect(() => {
      if (this.contactSeeded || !this.authed()) {
        return;
      }
      const p = this.profile.value();
      if (!p) {
        return;
      }
      this.contactSeeded = true;
      this.model.update((m) => ({
        ...m,
        contactName: m.contactName || (p.fullName ?? ''),
        phone: m.phone || (p.phoneNumber ?? ''),
      }));
      if (!this.email()) {
        this.email.set(p.email ?? '');
      }
    });

    // Prefill the delivery address from the customer's most recent order.
    effect(() => {
      if (this.addressSeeded || !this.authed()) {
        return;
      }
      const order = this.lastOrder.value();
      if (!order) {
        return;
      }
      this.addressSeeded = true;
      const a = order.shippingAddress;
      if (!a) {
        return;
      }
      this.model.update((m) => ({
        ...m,
        contactName: m.contactName || (a.contactName ?? ''),
        phone: m.phone || (a.phone ?? ''),
        area: m.area || (a.city ?? ''),
        addressDetail: m.addressDetail || (a.addressLine1 ?? ''),
        stateOrProvinceId:
          m.stateOrProvinceId || (a.stateOrProvinceId ? String(a.stateOrProvinceId) : ''),
        countryId: m.countryId || (a.countryId ?? ''),
      }));
      // Ensure the governorate <select> has its options so the prefilled value shows.
      if (a.countryId) {
        this.loadStates(a.countryId);
      }
    });

    // Single shipping country (Jordan): preselect it and load its governorates
    // so the customer only picks the governorate.
    effect(() => {
      const list = this.countries.value();
      if (list?.length === 1 && !this.model().countryId) {
        this.model.update((m) => ({ ...m, countryId: list[0].id }));
        this.loadStates(list[0].id);
      }
    });

    // Preselect the first enabled payment method once the list loads (keep any
    // choice the shopper already made if it's still available).
    effect(() => {
      const methods = this.payMethods();
      if (methods.length === 0) {
        return;
      }
      const current = this.pay();
      if (!current || !methods.some((m) => m.id === current)) {
        this.pay.set(methods[0].id);
      }
    });

    // Scroll to top whenever the stage changes (browser only).
    effect(() => {
      this.stage();
      if (this.isBrowser) {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }
    });

    // On the checkout stage, recompute shipping once the address is complete
    // (guests and signed-in shoppers alike).
    effect(() => {
      if (this.stage() === 'checkout' && this.addressValid()) {
        this.calculateShipping();
      } else if (!this.addressValid()) {
        this.selectedShipping.set(null);
      }
    });
  }

  private loadStates(countryId: string): void {
    this.locations.states(countryId).subscribe({
      next: (states) => this.states.set(states),
      error: () => this.states.set([]),
    });
  }

  protected goStage(stage: Stage): void {
    this.stage.set(stage);
  }

  /** Cart → checkout. Guests are taken to the checkout stage and gated there. */
  protected proceed(): void {
    if (this.cart.items().length === 0) {
      return;
    }
    this.stage.set('checkout');
  }

  protected total(): number {
    return (
      this.cart.subTotal() -
      this.cart.discount() +
      (this.selectedShipping()?.price ?? 0)
    );
  }

  /** Validate a promo code against the server cart (signed-in customers only). */
  protected applyCoupon(): void {
    const code = this.promo().trim();
    if (!code) {
      return;
    }
    if (!this.auth.isAuthenticated()) {
      this.toast.error(this.translate.instant('cart.coupon_signin'));
      return;
    }
    this.cart.applyCoupon(code);
    this.promo.set('');
  }

  protected removeCoupon(): void {
    this.cart.clearCoupon();
    this.promo.set('');
  }

  protected paidTotal(): number {
    const order = this.placedOrder();
    return order
      ? order.subTotalWithDiscount + order.shippingFeeAmount + order.taxAmount
      : 0;
  }

  protected setQty(id: number, quantity: number): void {
    if (quantity < 1) {
      this.remove(id);
      return;
    }
    this.busy.set(true);
    this.cart.update(id, { quantity }).subscribe({
      next: () => this.busy.set(false),
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }

  protected remove(id: number): void {
    this.busy.set(true);
    this.cart.remove(id).subscribe({
      next: () => this.busy.set(false),
      error: () => {
        this.busy.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }

  private address(): AddressDto {
    const m = this.model();
    return {
      contactName: m.contactName,
      phone: m.phone,
      addressLine1: m.addressDetail,
      city: m.area,
      stateOrProvinceId: Number(m.stateOrProvinceId),
      countryId: m.countryId,
    };
  }

  private calculateShipping(): void {
    // Guests post their cart lines (no server cart); signed-in shoppers use the server cart.
    const request$ = this.auth.isAuthenticated()
      ? this.checkout.shippingOptions({ shippingAddress: this.address() })
      : this.checkout.guestShippingOptions({
          shippingAddress: this.address(),
          items: this.guestItems(),
        });
    request$.subscribe({
      next: (options) => {
        this.shippingOptions.set(options);
        // The shopper must pick a carrier (each has its own rate). Keep their current choice if it's
        // still offered after a recompute; otherwise clear it so the dropdown forces a fresh pick.
        const current = this.selectedShipping();
        this.selectedShipping.set(
          current ? (options.find((o) => o.id === current.id) ?? null) : null,
        );
      },
      error: () => {
        this.shippingOptions.set([]);
        this.selectedShipping.set(null);
      },
    });
  }

  protected placeOrder(): void {
    const shipping = this.selectedShipping();
    if (!shipping || !this.canPlaceOrder() || this.placing()) {
      return;
    }
    this.placing.set(true);

    if (this.auth.isAuthenticated()) {
      const request: PlaceOrderRequest = {
        shippingAddress: this.address(),
        shippingMethodName: shipping.name ?? '',
        paymentMethod: this.pay(),
        orderNote: this.orderNote() || null,
        couponCode: this.cart.discount() > 0 ? this.cart.appliedCoupon() : null,
      };
      this.checkout.placeOrder(request).subscribe({
        next: (order) => {
          this.placedOrder.set(order);
          this.cart.reload();
          this.afterOrderPlaced(order);
        },
        error: () => {
          this.placing.set(false);
          this.toast.error(this.translate.instant('common.error'));
        },
      });
      return;
    }

    // Guest checkout — no account, no coupons; the email (when given) is the order's tracking secret.
    // Send null rather than "" when blank so the backend synthesizes a unique placeholder instead of 400ing.
    const guestRequest: GuestPlaceOrderRequest = {
      email: this.email().trim() || null,
      items: this.guestItems(),
      shippingAddress: this.address(),
      shippingMethodName: shipping.name ?? '',
      paymentMethod: this.pay(),
      orderNote: this.orderNote() || null,
    };
    this.checkout.guestPlaceOrder(guestRequest).subscribe({
      next: (order) => {
        this.placedOrder.set(order);
        this.cart.clearGuest();
        this.afterOrderPlaced(order);
      },
      error: () => {
        this.placing.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }

  /** Online gateways (everything except Cash on Delivery) go through initiate → pay → callback. */
  private requiresOnlinePayment(method: string): boolean {
    return !!method && method !== 'CoD';
  }

  /**
   * Routes after the order row is created. Cash on Delivery needs no payment — signed-in shoppers go
   * to their account, guests land on the confirmation screen (which shows their tracking number).
   * Online methods start a gateway payment and send the shopper to pay (a local sandbox mock page when
   * testing, or the gateway's hosted page in production); the return URL brings signed-in shoppers back
   * to their account and guests to the public track page (pre-filled with their tracking number).
   */
  private afterOrderPlaced(order: OrderDetailDto): void {
    const method = this.pay();
    const isGuest = !this.auth.isAuthenticated();
    // Guests have no account page; the public track page (pre-filled) is their landing spot.
    const returnUrl = isGuest
      ? `/track-order?number=${order.trackingNumber ?? ''}`
      : '/account';

    if (!this.requiresOnlinePayment(method)) {
      this.placing.set(false);
      this.toast.success(this.translate.instant('checkout.order_placed'));
      if (isGuest) {
        // Show the in-page confirmation with the tracking number.
        this.stage.set('done');
      } else {
        void this.router.navigateByUrl('/account');
      }
      return;
    }

    const initiate$ = isGuest
      ? this.payments.guestInitiate({
          orderId: order.id,
          method,
          returnUrl,
          // Use the email stored on the order (the backend synthesizes one when the guest left it blank),
          // so the gateway ownership check matches even for emailless guests.
          email: order.guestEmail ?? this.email().trim(),
        })
      : this.payments.initiate({ orderId: order.id, method, returnUrl });

    initiate$.subscribe({
      next: (res) => {
        this.placing.set(false);
        if (res.isSandbox) {
          void this.router.navigate(['/payment/mock'], {
            queryParams: {
              orderId: res.orderId,
              paymentId: res.paymentId,
              method: res.method,
              amount: order.orderTotal,
              returnUrl,
            },
          });
        } else if (this.isBrowser) {
          window.location.href = res.redirectUrl;
        }
      },
      error: () => {
        // The order exists but payment couldn't start — let them retry/track afterwards.
        this.placing.set(false);
        this.toast.error(this.translate.instant('checkout.payment_start_error'));
        if (isGuest) {
          this.stage.set('done');
        } else {
          void this.router.navigateByUrl('/account');
        }
      },
    });
  }

  protected finish(path: string): void {
    void this.router.navigateByUrl(path);
  }
}
