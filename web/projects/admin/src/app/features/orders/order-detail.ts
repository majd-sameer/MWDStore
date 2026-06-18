import { DatePipe } from '@angular/common';
import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  AdminOperationsService,
  AdminOrdersService,
  AdminWarehousesService,
  type AdminShipmentDto,
  type OrderDetailDto,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import {
  ORDER_STATUS,
  ORDER_STATUS_OPTIONS,
  orderStatusBadge,
} from '../../shared/order-status';

/**
 * Order detail: line items, addresses and totals, plus admin actions to change
 * the status (`PUT /status`) or cancel the order (`POST /cancel`, which restocks
 * server-side).
 */
@Component({
  selector: 'app-admin-order-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/orders" class="text-decoration-none">← {{ 'orders.title' | translate }}</a>
    </nav>

    @if (order.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (order.error()) {
      <div class="alert alert-danger">{{ 'order_detail.load_failed' | translate }}</div>
    } @else if (order.value(); as o) {
      <app-page-header
        [title]="'order_detail.title' | translate: { id: o.id }"
        [subtitle]="o.createdOn | date: 'medium'"
      >
        <span class="badge fs-6" [class]="badge(o.orderStatus)">
          {{ 'orders.status_' + o.orderStatus | translate }}
        </span>
      </app-page-header>

      <div class="row g-4">
        <div class="col-lg-8">
          <div class="card border-0 shadow-sm mb-4">
            <div class="card-header bg-body fw-semibold">{{ 'dashboard.col_items' | translate }}</div>
            <div class="card-body p-0">
              <table class="table align-middle mb-0">
                <thead>
                  <tr>
                    <th scope="col">{{ 'products.col_product' | translate }}</th>
                    <th scope="col" class="text-end">{{ 'products.col_price' | translate }}</th>
                    <th scope="col" class="text-end">{{ 'order_detail.col_qty' | translate }}</th>
                    <th scope="col" class="text-end">{{ 'order_detail.col_line_total' | translate }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (it of o.items ?? []; track it.productId) {
                    <tr>
                      <td>{{ it.productName }}</td>
                      <td class="text-end">{{ it.productPrice | money }}</td>
                      <td class="text-end">{{ it.quantity }}</td>
                      <td class="text-end">
                        {{ it.productPrice * it.quantity - it.discountAmount | money }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <div class="card border-0 shadow-sm mb-4">
            <div class="card-header bg-body fw-semibold">{{ 'order_detail.shipments_title' | translate }}</div>
            <div class="card-body">
              @for (s of shipments(); track s.id) {
                <div class="border rounded p-2 mb-2 small">
                  <div class="d-flex justify-content-between">
                    <span class="fw-medium">
                      {{ 'order_detail.shipment_label' | translate: { id: s.id, warehouse: s.warehouseName } }}
                    </span>
                    <span class="text-body-secondary">{{ s.createdOn | date: 'medium' }}</span>
                  </div>
                  <div class="text-body-secondary">
                    {{ 'order_detail.tracking' | translate }} {{ s.trackingNumber || '—' }} ·
                    @for (it of s.items; track it.id) {
                      <span class="me-2">{{ it.productName }} × {{ it.quantity }}</span>
                    }
                  </div>
                </div>
              } @empty {
                <p class="text-body-secondary small mb-2">{{ 'order_detail.no_shipments' | translate }}</p>
              }

              @if (unshippedItems().length) {
                <hr class="my-3" />
                <div class="fw-semibold small mb-2">{{ 'order_detail.create_shipment' | translate }}</div>
                <div class="row g-2 mb-2">
                  <div class="col-md-6">
                    <label class="form-label small" for="ship-warehouse">{{ 'inventory.warehouse' | translate }}</label>
                    <select id="ship-warehouse" class="form-select form-select-sm" #warehouseSel>
                      @for (w of warehouses.value() ?? []; track w.id) {
                        <option value="{{ w.id }}">{{ w.name }}</option>
                      }
                    </select>
                  </div>
                  <div class="col-md-6">
                    <label class="form-label small" for="ship-tracking">{{ 'order_detail.tracking_number' | translate }}</label>
                    <input id="ship-tracking" type="text" class="form-control form-control-sm" #trackingInput
                      [value]="o.trackingNumber || ''" disabled />
                  </div>
                </div>
                @for (it of unshippedItems(); track it.id) {
                  <div class="d-flex align-items-center gap-2 mb-1 small">
                    <span class="flex-grow-1">{{ it.name }}</span>
                    <span class="text-body-secondary">
                      {{ 'order_detail.remaining' | translate }} {{ it.remaining }}
                    </span>
                    <input type="number" class="form-control form-control-sm" style="width: 5rem"
                      min="0" [max]="it.remaining" [value]="shipQty()[it.id] ?? it.remaining"
                      (input)="setShipQty(it.id, $any($event.target).valueAsNumber)" />
                  </div>
                }
                <button type="button" class="btn btn-primary btn-sm mt-2"
                  [disabled]="saving() || !(warehouses.value() ?? []).length"
                  (click)="createShipment(o, warehouseSel.value, trackingInput.value)">
                  {{ (saving() ? 'common.saving' : 'order_detail.create_shipment') | translate }}
                </button>
                @if (!(warehouses.value() ?? []).length) {
                  <div class="small text-warning mt-1">{{ 'order_detail.create_warehouse_first' | translate }}</div>
                }
              }
            </div>
          </div>

          <div class="row g-4">
            <div class="col-md-6">
              <div class="card border-0 shadow-sm h-100">
                <div class="card-header bg-body fw-semibold">{{ 'order_detail.shipping_address' | translate }}</div>
                <div class="card-body small">
                  <div>{{ o.shippingAddress.contactName }}</div>
                  <div>{{ o.shippingAddress.phone }}</div>
                  <div>{{ o.shippingAddress.addressLine1 }}</div>
                  @if (o.shippingAddress.addressLine2) {
                    <div>{{ o.shippingAddress.addressLine2 }}</div>
                  }
                  <div>{{ o.shippingAddress.city }} {{ o.shippingAddress.zipCode }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-6">
              <div class="card border-0 shadow-sm h-100">
                <div class="card-header bg-body fw-semibold">{{ 'order_detail.billing_address' | translate }}</div>
                <div class="card-body small">
                  <div>{{ o.billingAddress.contactName }}</div>
                  <div>{{ o.billingAddress.phone }}</div>
                  <div>{{ o.billingAddress.addressLine1 }}</div>
                  @if (o.billingAddress.addressLine2) {
                    <div>{{ o.billingAddress.addressLine2 }}</div>
                  }
                  <div>{{ o.billingAddress.city }} {{ o.billingAddress.zipCode }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="col-lg-4">
          <div class="card border-0 shadow-sm mb-4">
            <div class="card-header bg-body fw-semibold">{{ 'order_detail.summary' | translate }}</div>
            <div class="card-body">
              <dl class="row mb-0 small">
                <dt class="col-7 fw-normal text-body-secondary">{{ 'order_detail.customer_id' | translate }}</dt>
                <dd class="col-5 text-end">{{ o.customerId }}</dd>
                <dt class="col-7 fw-normal text-body-secondary">{{ 'order_detail.subtotal' | translate }}</dt>
                <dd class="col-5 text-end">{{ o.subTotal | money }}</dd>
                <dt class="col-7 fw-normal text-body-secondary">{{ 'order_detail.discount' | translate }}</dt>
                <dd class="col-5 text-end">-{{ o.discountAmount | money }}</dd>
                <dt class="col-7 fw-normal text-body-secondary">{{ 'order_detail.tax' | translate }}</dt>
                <dd class="col-5 text-end">{{ o.taxAmount | money }}</dd>
                <dt class="col-7 fw-normal text-body-secondary">
                  {{ 'nav.shipping' | translate }} ({{ o.shippingMethod }})
                </dt>
                <dd class="col-5 text-end">{{ o.shippingFeeAmount | money }}</dd>
                <dt class="col-7 border-top pt-2 mt-2">{{ 'dashboard.col_total' | translate }}</dt>
                <dd class="col-5 border-top pt-2 mt-2 text-end fw-semibold">
                  {{ o.orderTotal | money }}
                </dd>
              </dl>
              @if (o.orderNote) {
                <hr />
                <div class="small">
                  <span class="text-body-secondary">{{ 'common.note' | translate }}:</span> {{ o.orderNote }}
                </div>
              }
            </div>
          </div>

          <div class="card border-0 shadow-sm">
            <div class="card-header bg-body fw-semibold">{{ 'order_detail.update_status' | translate }}</div>
            <div class="card-body">
              <div class="input-group mb-2">
                <select class="form-select" [value]="o.orderStatus" #sel>
                  @for (opt of statusOptions; track opt.value) {
                    <option [value]="opt.value">{{ 'orders.status_' + opt.value | translate }}</option>
                  }
                </select>
                <button
                  type="button"
                  class="btn btn-primary"
                  [disabled]="saving()"
                  (click)="updateStatus(o.id, sel.value)"
                >
                  {{ (saving() ? 'common.saving' : 'order_detail.apply') | translate }}
                </button>
              </div>
              <button
                type="button"
                class="btn btn-outline-danger btn-sm w-100"
                [disabled]="saving() || o.orderStatus === canceledStatus"
                (click)="cancel(o.id)"
              >
                {{ 'order_detail.cancel_order' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminOrderDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(AdminOrdersService);
  private readonly operations = inject(AdminOperationsService);
  private readonly warehousesService = inject(AdminWarehousesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly orderId = computed(() => Number(this.idParam().get('id')));
  protected readonly order = this.service.getResource(this.orderId);
  protected readonly warehouses = this.warehousesService.listResource();

  protected readonly statusOptions = ORDER_STATUS_OPTIONS;
  protected readonly badge = orderStatusBadge;
  protected readonly canceledStatus = ORDER_STATUS.Canceled;
  protected readonly saving = signal(false);

  protected readonly shipments = signal<AdminShipmentDto[]>([]);
  protected readonly shipQty = signal<Record<number, number>>({});

  /** Order items with shipped quantities subtracted; the create-shipment form lists these. */
  protected readonly unshippedItems = computed(() => {
    const o = this.order.value();
    if (!o) {
      return [];
    }
    const shippedByItem = new Map<number, number>();
    for (const shipment of this.shipments()) {
      for (const item of shipment.items) {
        shippedByItem.set(
          item.orderItemId,
          (shippedByItem.get(item.orderItemId) ?? 0) + item.quantity,
        );
      }
    }
    return (o.items ?? [])
      .map((it) => ({
        id: it.id,
        name: it.productName ?? '',
        remaining: it.quantity - (shippedByItem.get(it.id) ?? 0),
      }))
      .filter((it) => it.remaining > 0);
  });

  constructor() {
    effect(() => {
      const id = this.orderId();
      if (Number.isFinite(id)) {
        this.loadShipments(id);
      }
    });
  }

  private loadShipments(orderId: number): void {
    this.operations.shipments(orderId).subscribe({
      next: (items) => this.shipments.set(items),
      error: () => this.shipments.set([]),
    });
  }

  protected setShipQty(orderItemId: number, quantity: number): void {
    this.shipQty.update((q) => ({ ...q, [orderItemId]: Number.isFinite(quantity) ? quantity : 0 }));
  }

  protected createShipment(order: OrderDetailDto, warehouseId: string, tracking: string): void {
    const items = this.unshippedItems()
      .map((it) => ({
        orderItemId: it.id,
        quantity: Math.min(this.shipQty()[it.id] ?? it.remaining, it.remaining),
      }))
      .filter((it) => it.quantity > 0);
    if (!items.length) {
      this.toast.error(this.translate.instant('order_detail.nothing_to_ship'));
      return;
    }

    this.saving.set(true);
    this.operations
      .createShipment({
        orderId: order.id,
        warehouseId: Number(warehouseId),
        trackingNumber: tracking.trim() || null,
        items,
      })
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('order_detail.shipment_created'));
          this.saving.set(false);
          this.shipQty.set({});
          this.loadShipments(order.id);
          this.order.reload();
        },
        error: () => {
          this.toast.error(this.translate.instant('order_detail.shipment_create_failed'));
          this.saving.set(false);
        },
      });
  }

  protected updateStatus(id: number, value: string): void {
    this.saving.set(true);
    void firstValueFrom(this.service.updateStatus(id, { orderStatus: Number(value) }))
      .then(() => {
        this.toast.success(this.translate.instant('order_detail.status_updated'));
        this.order.reload();
      })
      .catch(() => this.toast.error(this.translate.instant('order_detail.status_update_failed')))
      .finally(() => this.saving.set(false));
  }

  protected cancel(id: number): void {
    if (!confirm(this.translate.instant('order_detail.confirm_cancel'))) {
      return;
    }
    this.saving.set(true);
    void firstValueFrom(this.service.cancel(id))
      .then(() => {
        this.toast.success(this.translate.instant('order_detail.cancelled_ok'));
        this.order.reload();
      })
      .catch(() => this.toast.error(this.translate.instant('order_detail.cancel_failed')))
      .finally(() => this.saving.set(false));
  }
}
