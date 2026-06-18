import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import type { OrderAddressDto, OrderDetailDto } from 'data-access';
import { statusLabel } from '../features/account/order-status';

/** Presentational rendering of a single order, shared by confirmation + account. */
@Component({
  selector: 'app-order-detail-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, TranslatePipe],
  template: `
    @if (order(); as order) {
      <div class="d-flex flex-wrap justify-content-between align-items-center mb-3">
        <div>
          <h2 class="h5 mb-0">{{ 'account.order_no' | translate: { id: order.id } }}</h2>
          <span class="text-body-secondary small">{{ order.createdOn | date: 'medium' : '' : locale() }}</span>
        </div>
        <span class="badge text-bg-secondary fs-6">{{
          statusLabel(order.orderStatus, order.orderStatusName) | translate
        }}</span>
      </div>

      <div class="table-responsive">
        <table class="table align-middle">
          <thead>
            <tr>
              <th scope="col">{{ 'orderview.product' | translate }}</th>
              <th scope="col" class="text-end">{{ 'orderview.price' | translate }}</th>
              <th scope="col" class="text-center">{{ 'orderview.qty' | translate }}</th>
              <th scope="col" class="text-end">{{ 'orderview.line_total' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of order.items ?? []; track item.productId) {
              <tr>
                <td>{{ item.productName }}</td>
                <td class="text-end tabular-nums">{{ item.productPrice | money }}</td>
                <td class="text-center tabular-nums">{{ item.quantity }}</td>
                <td class="text-end tabular-nums">{{ item.productPrice * item.quantity | money }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="row g-4">
        <div class="col-md-6">
          <h3 class="h6 text-uppercase text-body-secondary">{{ 'orderview.shipping_address' | translate }}</h3>
          <address class="mb-0">{{ formatAddress(order.shippingAddress) }}</address>
        </div>
        <div class="col-md-6">
          <dl class="row mb-0">
            <dt class="col-7 fw-normal text-body-secondary">{{ 'orderview.subtotal' | translate }}</dt>
            <dd class="col-5 text-end tabular-nums">{{ order.subTotal | money }}</dd>
            @if (order.discountAmount) {
              <dt class="col-7 fw-normal text-body-secondary">{{ 'orderview.discount' | translate }}</dt>
              <dd class="col-5 text-end text-success tabular-nums">−{{ order.discountAmount | money }}</dd>
            }
            <dt class="col-7 fw-normal text-body-secondary">
              {{ 'orderview.shipping' | translate
              }}{{ order.shippingMethod ? ' (' + order.shippingMethod + ')' : '' }}
            </dt>
            <dd class="col-5 text-end tabular-nums">{{ order.shippingFeeAmount | money }}</dd>
            @if (order.taxAmount) {
              <dt class="col-7 fw-normal text-body-secondary">{{ 'orderview.tax' | translate }}</dt>
              <dd class="col-5 text-end tabular-nums">{{ order.taxAmount | money }}</dd>
            }
            <dt class="col-7 border-top pt-2">{{ 'orderview.total' | translate }}</dt>
            <dd class="col-5 text-end border-top pt-2 fw-semibold tabular-nums">
              {{ order.orderTotal | money }}
            </dd>
          </dl>
        </div>
      </div>
    }
  `,
})
export class OrderDetailView {
  private readonly language = inject(LanguageService);
  readonly order = input.required<OrderDetailDto | undefined>();

  /** Active locale for date formatting; prices stay Western (en-US). */
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));

  /** Localized status label (i18n key by code, falling back to the raw backend name). */
  protected readonly statusLabel = statusLabel;

  protected formatAddress(address: OrderAddressDto): string {
    return [
      address.contactName,
      address.addressLine1,
      address.addressLine2,
      address.city,
      address.zipCode,
      address.countryId,
    ]
      .filter(Boolean)
      .join(', ');
  }
}
