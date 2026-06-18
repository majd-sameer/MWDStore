import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AdminInventoryService,
  type StockAdjustmentRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Inventory: look up a product's per-warehouse stock, then apply an adjustment.
 * The API treats `adjustedQuantity` as a signed delta (positive adds, negative
 * removes, clamped at zero) and mirrors the change back to the product total.
 */
@Component({
  selector: 'app-admin-inventory',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'inventory.title' | translate"
      [subtitle]="'inventory.subtitle' | translate"
    />

    <div class="row g-4">
      <div class="col-lg-7">
        <div class="card border-0 shadow-sm">
          <div class="card-body">
            <form class="row g-2 align-items-center mb-3" (submit)="lookup($event)">
              <div class="col-sm-4">
                <input
                  #pid
                  type="number"
                  min="1"
                  class="form-control"
                  [placeholder]="'inventory.product_id' | translate"
                  [value]="productId() ?? ''"
                  [attr.aria-label]="'inventory.product_id' | translate"
                />
              </div>
              <div class="col-sm-auto">
                <button class="btn btn-primary" type="submit">{{ 'inventory.look_up' | translate }}</button>
              </div>
            </form>

            @if (productId() === null) {
              <div class="alert alert-info mb-0">{{ 'inventory.enter_id' | translate }}</div>
            } @else if (stock.isLoading()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
                </div>
              </div>
            } @else if (stock.error()) {
              <div class="alert alert-danger mb-0">
                {{ 'inventory.no_stock' | translate: { id: productId() } }}
              </div>
            } @else if (stock.value(); as s) {
              <h2 class="h5">{{ s.productName }}</h2>
              <p class="text-body-secondary">
                {{ 'inventory.total_on_hand' | translate }} <strong>{{ s.productStockQuantity }}</strong>
              </p>
              <div class="table-responsive">
                <table class="table align-middle mb-0">
                  <thead>
                    <tr>
                      <th scope="col">{{ 'inventory.warehouse' | translate }}</th>
                      <th scope="col" class="text-end">{{ 'inventory.col_on_hand' | translate }}</th>
                      <th scope="col" class="text-end">{{ 'inventory.col_reserved' | translate }}</th>
                      <th scope="col" class="text-end">{{ 'inventory.col_available' | translate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (w of s.warehouses ?? []; track w.warehouseId) {
                      <tr>
                        <td>{{ w.warehouseName }} <span class="text-body-secondary">#{{ w.warehouseId }}</span></td>
                        <td class="text-end">{{ w.quantity }}</td>
                        <td class="text-end">{{ w.reservedQuantity }}</td>
                        <td class="text-end">{{ w.quantity - w.reservedQuantity }}</td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="4" class="text-center text-body-secondary py-4">
                          {{ 'inventory.no_warehouse_rows' | translate }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        </div>
      </div>

      <div class="col-lg-5">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'inventory.adjust_title' | translate }}</div>
          <div class="card-body">
            @if (!stock.value()?.warehouses?.length) {
              <p class="text-body-secondary mb-0">{{ 'inventory.lookup_hint' | translate }}</p>
            } @else {
              <div class="mb-3">
                <label for="adj-wh" class="form-label">{{ 'inventory.warehouse' | translate }}</label>
                <select
                  id="adj-wh"
                  class="form-select"
                  [value]="selectedWarehouse() ?? ''"
                  (change)="warehouseId.set(+$any($event.target).value)"
                >
                  @for (w of stock.value()?.warehouses ?? []; track w.warehouseId) {
                    <option [value]="w.warehouseId">{{ w.warehouseName }} (#{{ w.warehouseId }})</option>
                  }
                </select>
              </div>
              <div class="mb-3">
                <label for="adj-qty" class="form-label">{{ 'inventory.adjustment' | translate }}</label>
                <input
                  id="adj-qty"
                  type="number"
                  class="form-control"
                  [value]="delta()"
                  (input)="delta.set(+$any($event.target).value)"
                />
                <div class="form-text">{{ 'inventory.adjustment_hint' | translate }}</div>
              </div>
              <div class="mb-3">
                <label for="adj-note" class="form-label">{{ 'common.note' | translate }}</label>
                <input
                  id="adj-note"
                  type="text"
                  class="form-control"
                  [value]="note()"
                  (input)="note.set($any($event.target).value)"
                />
              </div>
              <button
                type="button"
                class="btn btn-primary w-100"
                [disabled]="saving() || selectedWarehouse() === null || !delta()"
                (click)="adjust()"
              >
                {{ (saving() ? 'inventory.applying' : 'inventory.apply') | translate }}
              </button>
            }
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminInventory {
  private readonly service = inject(AdminInventoryService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly productId = signal<number | null>(null);
  protected readonly stock = this.service.productStockResource(() =>
    this.productId() ?? 0,
  );

  protected readonly warehouseId = signal<number | null>(null);
  protected readonly delta = signal<number>(0);
  protected readonly note = signal<string>('');
  protected readonly saving = signal(false);

  // Effective selection: an explicit pick, else the first warehouse row.
  protected readonly selectedWarehouse = computed(
    () =>
      this.warehouseId() ??
      this.stock.value()?.warehouses?.[0]?.warehouseId ??
      null,
  );

  protected lookup(event: Event): void {
    event.preventDefault();
    const input = (event.target as HTMLFormElement).querySelector('input');
    const value = Number(input?.value);
    this.productId.set(Number.isFinite(value) && value > 0 ? value : null);
    this.warehouseId.set(null);
    this.delta.set(0);
    this.note.set('');
  }

  protected adjust(): void {
    const productId = this.productId();
    const warehouseId = this.selectedWarehouse();
    if (productId === null || warehouseId === null) {
      return;
    }
    const body: StockAdjustmentRequest = {
      productId,
      warehouseId,
      adjustedQuantity: Number(this.delta()),
      note: this.note() || null,
    };
    this.saving.set(true);
    void firstValueFrom(this.service.adjust(body))
      .then(() => {
        this.toast.success(this.translate.instant('inventory.adjusted_ok'));
        this.delta.set(0);
        this.note.set('');
        this.stock.reload();
      })
      .catch(() => this.toast.error(this.translate.instant('inventory.adjust_failed')))
      .finally(() => this.saving.set(false));
  }
}
