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
  templateUrl: './inventory.html',
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
