import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AdminInventoryService,
  SALES_CHANNEL,
  STOCK_OUT_REASON,
  type StockOutRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

const REASONS = [
  { value: STOCK_OUT_REASON.Sale, key: 'sale' },
  { value: STOCK_OUT_REASON.Gift, key: 'gift' },
  { value: STOCK_OUT_REASON.Matched, key: 'matched' },
  { value: STOCK_OUT_REASON.ThirdParty, key: 'thirdParty' },
  { value: STOCK_OUT_REASON.ExternalEvent, key: 'externalEvent' },
  { value: STOCK_OUT_REASON.Reserved, key: 'reserved' },
  { value: STOCK_OUT_REASON.DisplayOnly, key: 'displayOnly' },
];

const CHANNELS = [
  { value: SALES_CHANNEL.Showroom, key: 'showroom' },
  { value: SALES_CHANNEL.ExternalExhibition, key: 'externalExhibition' },
  { value: SALES_CHANNEL.ExternalBroker, key: 'externalBroker' },
  { value: SALES_CHANNEL.LocalBroker, key: 'localBroker' },
  { value: SALES_CHANNEL.OnlineStore, key: 'onlineStore' },
];

/** Reasons that show the channel select (Sale requires it; the others are optional). */
const CHANNEL_REASONS: number[] = [
  STOCK_OUT_REASON.Sale,
  STOCK_OUT_REASON.ExternalEvent,
  STOCK_OUT_REASON.ThirdParty,
];

/**
 * Stock-out form: look up a product's per-warehouse stock, then record a removal with a business
 * reason, an optional sales channel (required for Sale), a recipient/reference and a note. Quantity
 * is validated against on-hand both here and server-side.
 */
@Component({
  selector: 'app-admin-stock-out',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgSelectModule, TranslatePipe, PageHeader],
  templateUrl: './stock-out.html',
})
export class AdminStockOut {
  private readonly service = inject(AdminInventoryService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly reasons = REASONS;
  protected readonly channels = CHANNELS;

  protected readonly productId = signal<number | null>(null);
  protected readonly stock = this.service.productStockResource(
    () => this.productId() ?? 0,
  );

  protected readonly warehouseId = signal<number | null>(null);
  protected readonly quantity = signal<number>(1);
  protected readonly reason = signal<number>(STOCK_OUT_REASON.Sale);
  protected readonly channel = signal<number | null>(SALES_CHANNEL.Showroom);
  protected readonly recipient = signal<string>('');
  protected readonly note = signal<string>('');
  protected readonly saving = signal(false);

  protected readonly selectedWarehouse = computed(
    () =>
      this.warehouseId() ??
      this.stock.value()?.warehouses?.[0]?.warehouseId ??
      null,
  );

  protected readonly onHand = computed(() => {
    const id = this.selectedWarehouse();
    return (
      this.stock.value()?.warehouses?.find((w) => w.warehouseId === id)
        ?.quantity ?? 0
    );
  });

  protected readonly channelVisible = computed(() =>
    CHANNEL_REASONS.includes(this.reason()),
  );

  protected readonly channelRequired = computed(
    () => this.reason() === STOCK_OUT_REASON.Sale,
  );

  /** i18n key of the first failing rule, or null when the form is submittable. */
  protected readonly errorKey = computed<string | null>(() => {
    if (this.productId() === null || this.selectedWarehouse() === null) {
      return 'stockout.err_product';
    }
    const q = Number(this.quantity());
    if (!Number.isFinite(q) || q < 1) {
      return 'stockout.err_qty';
    }
    if (q > this.onHand()) {
      return 'stockout.err_overstock';
    }
    if (this.channelRequired() && this.channel() === null) {
      return 'stockout.err_channel';
    }
    return null;
  });

  protected lookup(event: Event): void {
    event.preventDefault();
    const input = (event.target as HTMLFormElement).querySelector('input');
    const value = Number(input?.value);
    this.productId.set(Number.isFinite(value) && value > 0 ? value : null);
    this.warehouseId.set(null);
    this.quantity.set(1);
    this.recipient.set('');
    this.note.set('');
  }

  protected setWarehouse(value: number | null): void {
    this.warehouseId.set(value);
  }

  protected setReason(value: number): void {
    this.reason.set(value);
    if (this.channelVisible() && this.channel() === null) {
      this.channel.set(SALES_CHANNEL.Showroom);
    }
  }

  protected setChannel(value: number | null): void {
    this.channel.set(value);
  }

  protected submit(): void {
    const productId = this.productId();
    const warehouseId = this.selectedWarehouse();
    if (productId === null || warehouseId === null || this.errorKey() !== null) {
      return;
    }

    const body: StockOutRequest = {
      productId,
      warehouseId,
      quantity: Number(this.quantity()),
      reason: this.reason(),
      channel: this.channelVisible() ? this.channel() : null,
      recipientOrRef: this.recipient() || null,
      note: this.note() || null,
    };

    this.saving.set(true);
    void firstValueFrom(this.service.stockOut(body))
      .then(() => {
        this.toast.success(this.translate.instant('stockout.done'));
        this.quantity.set(1);
        this.recipient.set('');
        this.note.set('');
        this.stock.reload();
      })
      .catch((err: { error?: { error?: string } }) =>
        this.toast.error(
          err?.error?.error ?? this.translate.instant('stockout.failed'),
        ),
      )
      .finally(() => this.saving.set(false));
  }
}
