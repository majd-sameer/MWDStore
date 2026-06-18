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
  template: `
    <main class="wrap cart-screen">
      <!-- Steps indicator (shown on every stage) -->
      <ol class="steps" aria-hidden="true">
        <li class="step" [class.is-on]="stage() === 'cart'" [class.is-done]="stage() !== 'cart'">
          <span class="n">
            @if (stage() !== 'cart') { <lib-icon name="check" [size]="15" /> } @else { 1 }
          </span>
          <span class="lbl">{{ 'checkout.step_cart' | translate }}</span>
        </li>
        <span class="bar"></span>
        <li
          class="step"
          [class.is-on]="stage() === 'checkout'"
          [class.is-done]="stage() === 'done'"
        >
          <span class="n">
            @if (stage() === 'done') { <lib-icon name="check" [size]="15" /> } @else { 2 }
          </span>
          <span class="lbl">{{ 'checkout.step_checkout' | translate }}</span>
        </li>
        <span class="bar"></span>
        <li class="step" [class.is-on]="stage() === 'done'">
          <span class="n">3</span>
          <span class="lbl">{{ 'checkout.step_done' | translate }}</span>
        </li>
      </ol>

      <!-- ===== Confirmation stage ===== -->
      @if (stage() === 'done') {
        <div class="confirm">
          <span class="confirm-ic"><lib-icon name="check" [size]="48" /></span>
          <h1 class="confirm-title">{{ 'confirmation.confirmed' | translate }}</h1>
          <p class="confirm-sub">{{ 'confirmation.support' | translate }}</p>

          <div class="confirm-card">
            <div class="sumrow">
              <span>{{ 'confirmation.order_label' | translate }}</span>
              <b class="tabular-nums">#{{ placedOrder()?.id }}</b>
            </div>
            @if (placedOrder()?.trackingNumber; as tn) {
              <div class="sumrow">
                <span>{{ 'confirmation.tracking_label' | translate }}</span>
                <b class="tabular-nums">{{ tn }}</b>
              </div>
            }
            <div class="sumrow">
              <span>{{ 'confirmation.delivery' | translate }}</span>
              <b>{{ 'confirmation.delivery_eta' | translate }}</b>
            </div>
            <div class="sumrow">
              <span>{{ 'checkout.payment_method' | translate }}</span>
              <b>{{ selectedMethodLabel() | translate }}</b>
            </div>
            <div class="sumrow total">
              <span>{{ 'confirmation.total_paid' | translate }}</span>
              <strong class="tabular-nums">{{ paidTotal() | money }}</strong>
            </div>
          </div>

          @if (!auth.isAuthenticated() && placedOrder()?.trackingNumber) {
            <p class="confirm-track-note">{{ 'confirmation.guest_track_note' | translate }}</p>
          }

          <div class="confirm-ctas">
            @if (!auth.isAuthenticated() && placedOrder()?.trackingNumber; as tn) {
              <a libButton variant="primary" size="lg" routerLink="/track-order"
                [queryParams]="{ number: tn }">
                <lib-icon name="search" [size]="18" /> {{ 'confirmation.track_cta' | translate }}
              </a>
              <button libButton variant="secondary" [outline]="true" size="lg" (click)="finish('/shop')">
                {{ 'confirmation.continue' | translate }}
              </button>
            } @else {
              <button libButton variant="primary" size="lg" (click)="finish('/')">
                {{ 'confirmation.home' | translate }}
              </button>
              <button libButton variant="secondary" [outline]="true" size="lg" (click)="finish('/shop')">
                {{ 'confirmation.continue' | translate }}
              </button>
            }
          </div>
        </div>
      }

      <!-- ===== Empty cart (cart/checkout stages with no lines) ===== -->
      @else if (cart.items().length === 0) {
        <div class="emptycart">
          <span class="emptycart-ic"><lib-icon name="bag" [size]="42" /></span>
          <h2 class="emptycart-title">{{ 'cart.empty' | translate }}</h2>
          <p class="emptycart-sub">{{ 'cart.empty_sub' | translate }}</p>
          <a libButton variant="primary" size="lg" routerLink="/shop">
            {{ 'cart.browse' | translate }} <lib-icon name="arrowEnd" [size]="18" />
          </a>
        </div>
      }

      <!-- ===== Cart + Checkout stages ===== -->
      @else {
        <div class="cartwrap">
          <div class="cart-main">
            <!-- Cart stage: line items -->
            @if (stage() === 'cart') {
              <div class="formcard formcard--lines">
                @for (item of cart.items(); track item.id) {
                  <div class="citem">
                    <a class="citem-media" [routerLink]="['/products', item.productId]">
                      <lib-tile [src]="item.productImageUrl" [seed]="item.productName ?? item.productId"
                        [alt]="item.productName" ratio="1x1" />
                    </a>
                    <div class="citem-body">
                      <a class="citem-name" [routerLink]="['/products', item.productId]">
                        {{ item.productName }}
                      </a>
                      @if (!item.isProductAvailableToOrder) {
                        <span class="citem-warn">{{ 'cart.unavailable' | translate }}</span>
                      }
                      <button type="button" class="citem-rm" [disabled]="busy()"
                        (click)="remove(item.id)">
                        <lib-icon name="trash" [size]="15" /> {{ 'cart.remove' | translate }}
                      </button>
                    </div>
                    <div class="citem-end">
                      <span class="citem-price tabular-nums">
                        {{ item.calculatedProductPrice.price * item.quantity | money }}
                      </span>
                      <div class="qty">
                        <button type="button" [disabled]="busy()"
                          [attr.aria-label]="'cart.decrease' | translate"
                          (click)="setQty(item.id, item.quantity - 1)">
                          <lib-icon name="minus" [size]="16" />
                        </button>
                        <span class="tabular-nums">{{ item.quantity }}</span>
                        <button type="button"
                          [disabled]="busy() || item.quantity >= (item.productStockQuantity || 99)"
                          [attr.aria-label]="'cart.increase' | translate"
                          (click)="setQty(item.id, item.quantity + 1)">
                          <lib-icon name="plus" [size]="16" />
                        </button>
                      </div>
                    </div>
                  </div>
                }
                <div class="cart-continue">
                  <a class="link-btn" routerLink="/shop">
                    <lib-icon name="arrowStart" [size]="15" /> {{ 'cart.continue' | translate }}
                  </a>
                </div>
              </div>
            }

            <!-- Checkout stage: forms (guest or signed-in) -->
            @else {
              @if (!auth.isAuthenticated()) {
                <div class="guest-banner">
                  <lib-icon name="user" [size]="18" />
                  <span>
                    {{ 'checkout.guest_note' | translate }}
                    <a routerLink="/login" [queryParams]="{ returnUrl: '/checkout' }">
                      {{ 'checkout.guest_signin' | translate }}
                    </a>
                  </span>
                </div>
              }
              <!-- Card 1 — contact -->
              <section class="formcard">
                <h3 class="card-h"><span class="card-ic"><lib-icon name="user" [size]="18" /></span>
                  {{ 'checkout.contact' | translate }}</h3>
                <div class="field2">
                  <div class="field">
                    <label for="ck-name">{{ 'checkout.full_name' | translate }}
                      <span class="req" aria-hidden="true">*</span></label>
                    <input id="ck-name" dir="auto" [formField]="f.contactName"
                      [placeholder]="'checkout.full_name' | translate" />
                  </div>
                  <div class="field">
                    <label for="ck-phone">{{ 'checkout.phone' | translate }}
                      <span class="req" aria-hidden="true">*</span></label>
                    <input id="ck-phone" dir="auto" inputmode="tel" [formField]="f.phone"
                      [placeholder]="'checkout.phone_ph' | translate" />
                  </div>
                </div>
                <div class="field">
                  <label for="ck-email">{{ 'checkout.email' | translate }}</label>
                  <input id="ck-email" type="email" dir="auto" placeholder="name@example.com"
                    [value]="email()" (input)="email.set($any($event.target).value)" />
                  @if (!auth.isAuthenticated()) {
                    @if (email() && !emailValid()) {
                      <small class="field-err">{{ 'checkout.email_invalid' | translate }}</small>
                    } @else {
                      <small class="field-hint">{{ 'checkout.email_guest_hint' | translate }}</small>
                    }
                  }
                </div>
              </section>

              <!-- Card 2 — delivery address -->
              <section class="formcard">
                <h3 class="card-h"><span class="card-ic"><lib-icon name="truck" [size]="18" /></span>
                  {{ 'checkout.address_title' | translate }}</h3>
                <div class="field2">
                  <div class="field">
                    <label for="ck-gov">{{ 'checkout.state' | translate }}
                      <span class="req" aria-hidden="true">*</span></label>
                    <select id="ck-gov" [formField]="f.stateOrProvinceId">
                      <option value="">{{ 'checkout.choose' | translate }}</option>
                      @for (s of states(); track s.id) {
                        <option value="{{ s.id }}">{{ s.name }}</option>
                      }
                    </select>
                  </div>
                  <div class="field">
                    <label for="ck-area">{{ 'checkout.area' | translate }}
                      <span class="req" aria-hidden="true">*</span></label>
                    <input id="ck-area" dir="auto" [formField]="f.area"
                      [placeholder]="'checkout.area_ph' | translate" />
                  </div>
                </div>
                <div class="field">
                  <label for="ck-addr">{{ 'checkout.address_detail' | translate }}</label>
                  <input id="ck-addr" dir="auto" [formField]="f.addressDetail"
                    [placeholder]="'checkout.address_detail_ph' | translate" />
                </div>
                <div class="field">
                  <label for="ck-note">{{ 'checkout.note' | translate }}</label>
                  <textarea id="ck-note" rows="2" dir="auto"
                    [placeholder]="'checkout.note_ph' | translate"
                    [value]="orderNote()" (input)="orderNote.set($any($event.target).value)"></textarea>
                </div>
              </section>

              <!-- Card 3 — shipping method (required: each carrier has its own rate table) -->
              <section class="formcard">
                <h3 class="card-h"><span class="card-ic"><lib-icon name="truck" [size]="18" /></span>
                  {{ 'checkout.method' | translate }}</h3>
                @if (!addressValid()) {
                  <p class="pay-empty">{{ 'checkout.shipping_need_address' | translate }}</p>
                } @else if (shippingOptions().length === 0) {
                  <p class="pay-empty">{{ 'checkout.no_shipping' | translate }}</p>
                } @else {
                  <div class="field">
                    <label for="ck-ship">{{ 'checkout.carrier' | translate }}
                      <span class="req" aria-hidden="true">*</span></label>
                    <select id="ck-ship" [value]="selectedShipping()?.id ?? ''"
                      (change)="onShippingChange($any($event.target).value)">
                      <option value="" disabled>{{ 'checkout.choose_shipping' | translate }}</option>
                      @for (o of shippingOptions(); track o.id) {
                        <option value="{{ o.id }}">
                          {{ shipLabel(o) | translate }} — {{ o.price | money }}
                        </option>
                      }
                    </select>
                  </div>
                }
              </section>

              <!-- Card 4 — payment method -->
              <section class="formcard">
                <h3 class="card-h"><span class="card-ic"><lib-icon name="lock" [size]="18" /></span>
                  {{ 'checkout.payment_method' | translate }}</h3>
                @if (paymentMethods.isLoading()) {
                  <p class="pay-empty">{{ 'common.loading' | translate }}</p>
                } @else if (payMethods().length === 0) {
                  <p class="pay-empty">{{ 'checkout.no_payment_methods' | translate }}</p>
                } @else {
                  @for (m of payMethods(); track m.id) {
                    <label class="payopt" [class.is-on]="pay() === m.id">
                      <input type="radio" name="pay" [checked]="pay() === m.id"
                        (change)="pay.set(m.id)" />
                      <span class="payopt-txt">
                        <b>{{ payLabel(m) | translate }}</b>
                      </span>
                      <lib-icon [name]="payIcon(m.id)" [size]="18" />
                    </label>
                  }
                }
                <button type="button" class="link-btn back-to-cart" (click)="goStage('cart')">
                  <lib-icon name="arrowStart" [size]="15" /> {{ 'checkout.back' | translate }}
                </button>
              </section>
            }
          </div>

          <!-- Shared order summary -->
          <aside class="summary">
            <h3 class="summary-h">{{ 'checkout.summary' | translate }}</h3>
            <div class="sumrow">
              <span>{{ 'cart.subtotal' | translate }}
                ({{ 'cart.count' | translate: { count: cart.count() } }})</span>
              <span class="tabular-nums">{{ cart.subTotal() | money }}</span>
            </div>
            <div class="sumrow">
              <span>{{ 'cart.shipping' | translate }}</span>
              @if (selectedShipping(); as s) {
                @if (s.price === 0) {
                  <b class="free">{{ 'cart.free' | translate }}</b>
                } @else {
                  <span class="tabular-nums">{{ s.price | money }}</span>
                }
              } @else {
                <span class="muted">{{ 'cart.shipping_note' | translate }}</span>
              }
            </div>

            @if (cart.discount() > 0) {
              <div class="sumrow">
                <span>{{ 'cart.discount' | translate }}</span>
                <span class="free tabular-nums">−{{ cart.discount() | money }}</span>
              </div>
            }

            @if (stage() === 'cart') {
              @if (cart.discount() > 0) {
                <div class="promo-applied">
                  <span class="promo-code">
                    <lib-icon name="check" [size]="15" /> {{ cart.appliedCoupon() }}
                  </span>
                  <button type="button" class="link-btn" (click)="removeCoupon()">
                    {{ 'cart.remove' | translate }}
                  </button>
                </div>
              } @else {
                <div class="promo">
                  <input [placeholder]="'cart.promo_ph' | translate" [value]="promo()"
                    (input)="promo.set($any($event.target).value)"
                    (keydown.enter)="applyCoupon()" />
                  <button libButton variant="dark" type="button"
                    [disabled]="!promo().trim() || cart.isLoading()" (click)="applyCoupon()">
                    {{ 'cart.apply' | translate }}
                  </button>
                </div>
                @if (cart.couponError(); as couponErr) {
                  <p class="promo-error">{{ couponErr }}</p>
                }
              }
            }

            <div class="sumrow total">
              <span>{{ 'cart.total' | translate }}</span>
              <strong class="tabular-nums">{{ total() | money }}</strong>
            </div>

            @if (stage() === 'cart') {
              <button libButton variant="primary" size="lg" [block]="true" class="summary-cta"
                (click)="proceed()">
                {{ 'checkout.proceed' | translate }} <lib-icon name="arrowEnd" [size]="18" />
              </button>
            } @else {
              <button libButton variant="primary" size="lg" [block]="true" class="summary-cta"
                [disabled]="!canPlaceOrder() || placing()" (click)="placeOrder()">
                <lib-icon name="lock" [size]="18" />
                {{ (placing() ? 'checkout.placing' : 'checkout.confirm_pay') | translate }}
              </button>
            }

            <div class="note">
              <lib-icon name="hands" [size]="18" />
              <span>{{ 'checkout.mission_note' | translate }}</span>
            </div>
          </aside>
        </div>
      }
    </main>
  `,
  styles: `
    :host {
      display: block;
    }
    .cart-screen {
      padding-block: 30px 60px;
    }

    /* ----- Steps indicator ----- */
    .steps {
      display: flex;
      align-items: center;
      gap: 8px;
      list-style: none;
      margin: 0 0 26px;
      padding: 0;
    }
    .step {
      display: flex;
      align-items: center;
      gap: 9px;
      color: var(--ink-3);
      font-weight: 600;
    }
    .step.is-on {
      color: var(--ink);
    }
    .step.is-done {
      color: var(--green-strong);
    }
    .step .n {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 30px;
      block-size: 30px;
      border-radius: 50%;
      border: 2px solid currentColor;
      font-size: 0.9rem;
    }
    .step.is-on .n {
      background: var(--navy);
      border-color: var(--navy);
      color: #fff;
    }
    .step.is-done .n {
      background: var(--green);
      border-color: var(--green);
      color: #fff;
    }
    .steps .bar {
      flex: 1;
      block-size: 2px;
      min-inline-size: 20px;
      background: var(--line-strong);
    }
    /* Keep all three steps on screen on narrow phones (no clipped "Confirm"). */
    @media (max-width: 480px) {
      .steps {
        gap: 5px;
      }
      .step {
        gap: 5px;
        font-size: 0.78rem;
      }
      .step .n {
        inline-size: 24px;
        block-size: 24px;
        font-size: 0.78rem;
      }
      .steps .bar {
        min-inline-size: 4px;
      }
    }

    /* ----- Layout ----- */
    .cartwrap {
      display: grid;
      grid-template-columns: 1fr 380px;
      gap: 34px;
      align-items: start;
    }
    @media (max-width: 980px) {
      .cartwrap {
        grid-template-columns: 1fr;
      }
    }
    .cart-main {
      display: flex;
      flex-direction: column;
      gap: 20px;
      min-inline-size: 0;
    }
    .formcard {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 24px 26px;
    }
    .formcard--lines {
      padding-block: 6px;
    }

    /* ----- Cart line items ----- */
    .citem {
      display: grid;
      grid-template-columns: 110px 1fr auto;
      gap: 18px;
      align-items: center;
      padding-block: 18px;
      border-block-end: 1px solid var(--line);
    }
    .citem:last-of-type {
      border-block-end: 0;
    }
    .citem-media {
      display: block;
      inline-size: 110px;
      border-radius: var(--r);
      overflow: hidden;
    }
    .citem-body {
      display: flex;
      flex-direction: column;
      gap: 8px;
      align-items: flex-start;
    }
    .citem-name {
      font-weight: 600;
      font-size: 1.05rem;
      color: var(--ink);
      text-decoration: none;
    }
    .citem-name:hover {
      color: var(--accent);
    }
    .citem-warn {
      font-size: 0.82rem;
      color: var(--danger, #b0492c);
    }
    .citem-rm {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      border: 0;
      background: transparent;
      padding: 0;
      color: var(--ink-3);
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .citem-rm:hover {
      color: #b0492c;
    }
    .citem-end {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 14px;
    }
    .citem-price {
      font-weight: 700;
      font-size: 1.05rem;
    }
    .qty {
      display: inline-flex;
      align-items: center;
      gap: 14px;
      border: 1px solid var(--line);
      border-radius: 999px;
      padding: 6px 12px;
    }
    .qty button {
      display: inline-flex;
      border: 0;
      background: transparent;
      color: var(--ink);
      cursor: pointer;
      padding: 0;
    }
    .qty button:disabled {
      color: var(--ink-3);
      cursor: not-allowed;
    }
    .cart-continue {
      padding: 18px 0;
    }
    .link-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      border: 0;
      background: transparent;
      padding: 0;
      color: var(--navy);
      font-weight: 600;
      cursor: pointer;
      text-decoration: none;
    }
    .link-btn:hover {
      color: var(--navy-deep);
    }
    .back-to-cart {
      margin-block-start: 10px;
    }

    /* ----- Guest banner ----- */
    .guest-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 12px 16px;
      border-radius: var(--r);
      background: var(--surface-2);
      color: var(--ink-2);
      font-size: 0.9rem;
    }
    .guest-banner a {
      color: var(--navy);
      font-weight: 600;
    }

    /* ----- Field hints / errors ----- */
    .field-hint {
      display: block;
      margin-block-start: 6px;
      font-size: 0.8rem;
      color: var(--ink-3);
    }
    .field-err {
      display: block;
      margin-block-start: 6px;
      font-size: 0.8rem;
      color: #b0492c;
    }
    .confirm-track-note {
      color: var(--ink-2);
      font-size: 0.92rem;
      margin-block: 0.25rem 0;
    }

    /* ----- Form cards ----- */
    .card-h {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 1.1rem;
      font-weight: 700;
      margin-block-end: 1.1rem;
    }
    .card-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 34px;
      block-size: 34px;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--accent);
    }
    .field {
      margin-block-end: 14px;
    }
    .field:last-child {
      margin-block-end: 0;
    }
    .field label {
      display: block;
      font-size: 0.88rem;
      font-weight: 600;
      margin-block-end: 7px;
    }
    .req {
      color: var(--danger, #b0492c);
      font-weight: 700;
      margin-inline-start: 2px;
    }
    .field input,
    .field select,
    .field textarea {
      inline-size: 100%;
      border: 1.5px solid var(--line);
      border-radius: var(--r-sm);
      padding: 12px 14px;
      background: var(--surface);
      color: var(--ink);
      font: inherit;
    }
    .field input:focus,
    .field select:focus,
    .field textarea:focus {
      outline: none;
      border-color: var(--navy);
    }
    .field2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 14px;
    }
    @media (max-width: 600px) {
      .field2 {
        grid-template-columns: 1fr;
      }
    }

    /* ----- Payment options ----- */
    .payopt {
      display: flex;
      align-items: center;
      gap: 12px;
      border: 1.5px solid var(--line);
      border-radius: var(--r);
      padding: 15px 16px;
      cursor: pointer;
      margin-block-end: 10px;
    }
    .payopt.is-on {
      border-color: var(--green);
      background: var(--green-soft);
    }
    .payopt input {
      accent-color: var(--green-strong);
    }
    .payopt-txt {
      flex: 1;
      display: flex;
      flex-direction: column;
      line-height: 1.4;
    }
    .payopt-txt span {
      font-size: 0.85rem;
      color: var(--ink-2);
    }
    .pay-empty {
      margin: 0;
      color: var(--ink-2);
      font-size: 0.9rem;
    }

    /* ----- Order summary ----- */
    .summary {
      position: sticky;
      inset-block-start: calc(68px + 16px);
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 24px;
    }
    @media (max-width: 980px) {
      .summary {
        position: static;
      }
    }
    .summary-h {
      font-size: 1.15rem;
      font-weight: 700;
      margin-block-end: 0.5rem;
    }
    .sumrow {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding-block: 9px;
      color: var(--ink-2);
    }
    .sumrow .free {
      color: var(--green-strong);
    }
    .sumrow .muted {
      font-size: 0.85rem;
      color: var(--ink-3);
    }
    .sumrow.total {
      margin-block-start: 10px;
      padding-block-start: 16px;
      border-block-start: 1px solid var(--line);
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--ink);
    }
    .promo {
      display: flex;
      gap: 8px;
      margin-block: 16px;
    }
    .promo input {
      flex: 1;
      min-inline-size: 0;
      border: 1.5px solid var(--line);
      border-radius: var(--r-sm);
      padding: 10px 12px;
      background: var(--surface);
      color: var(--ink);
      font: inherit;
    }
    .promo input:focus {
      outline: none;
      border-color: var(--navy);
    }
    .promo-applied {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      margin-block: 16px;
      padding: 10px 14px;
      border-radius: var(--r-sm);
      background: var(--green-soft);
      color: var(--green-strong);
      font-weight: 600;
    }
    .promo-code {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .promo-error {
      margin-block: 8px 0;
      font-size: 0.85rem;
      color: #b0492c;
    }
    .summary-cta {
      margin-block-start: 18px;
    }
    .note {
      display: flex;
      align-items: flex-start;
      gap: 9px;
      margin-block-start: 16px;
      padding: 12px 14px;
      border-radius: var(--r-sm);
      background: var(--green-soft);
      color: var(--green-strong);
      font-size: 0.9rem;
    }

    /* ----- Empty cart ----- */
    .emptycart {
      text-align: center;
      padding-block: 70px;
    }
    .emptycart-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 90px;
      block-size: 90px;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--accent);
      margin-block-end: 1.25rem;
    }
    .emptycart-title {
      font-weight: 700;
      font-size: 1.6rem;
    }
    .emptycart-sub {
      color: var(--ink-2);
      margin-block: 0.5rem 1.5rem;
    }

    /* ----- Confirmation ----- */
    .confirm {
      text-align: center;
      max-inline-size: 560px;
      margin-inline: auto;
      padding-block: 40px;
    }
    .confirm-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 96px;
      block-size: 96px;
      border-radius: 50%;
      background: var(--green);
      color: #fff;
      box-shadow: var(--sh-green);
      margin-block-end: 26px;
    }
    .confirm-title {
      font-weight: 700;
      font-size: clamp(1.8rem, 4vw, 2.4rem);
    }
    .confirm-sub {
      color: var(--ink-2);
      margin-block: 0.75rem 1.75rem;
    }
    .confirm-card {
      text-align: start;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 20px 24px;
    }
    .confirm-ctas {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      justify-content: center;
      margin-block-start: 28px;
    }
  `,
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
