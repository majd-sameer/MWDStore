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
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
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
  imports: [MoneyPipe, DatePipe, FormsModule, NgSelectModule, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './order-detail.html',
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

  /** Warehouse chosen for a new shipment; falls back to the first warehouse. */
  private readonly shipWarehouseId = signal<number | null>(null);
  protected readonly selectedShipWarehouse = computed(
    () => this.shipWarehouseId() ?? this.warehouses.value()?.[0]?.id ?? null,
  );
  protected setShipWarehouse(id: number | null): void {
    this.shipWarehouseId.set(id);
  }

  /** Status picked in the update-status ng-select; seeded from the loaded order. */
  protected readonly statusChoice = signal<number | null>(null);

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
    // Seed the status dropdown from the (re)loaded order so it always shows the
    // current status; picking a new one overrides until the next reload.
    effect(() => {
      const o = this.order.value();
      if (o) {
        this.statusChoice.set(o.orderStatus);
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

  protected createShipment(order: OrderDetailDto, warehouseId: number | null, tracking: string): void {
    if (warehouseId === null) {
      return;
    }
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
        warehouseId,
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

  protected updateStatus(id: number, value: number | null): void {
    if (value === null) {
      return;
    }
    this.saving.set(true);
    void firstValueFrom(this.service.updateStatus(id, { orderStatus: value }))
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
