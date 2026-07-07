import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AdminInventoryService,
  type AdminStockOutQuery,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon } from 'ui';
import { PageHeader } from '../../shared/page-header';

const PAGE_SIZE = 50;

const REASON_KEYS: Record<number, string> = {
  1: 'sale',
  2: 'gift',
  3: 'matched',
  4: 'thirdParty',
  5: 'externalEvent',
  6: 'reserved',
  7: 'displayOnly',
};

const CHANNEL_KEYS: Record<number, string> = {
  1: 'showroom',
  2: 'externalExhibition',
  3: 'externalBroker',
  4: 'localBroker',
  5: 'onlineStore',
};

/**
 * Stock-out log: paged, filterable view of tracked stock removals (StockHistory rows carrying a
 * reason). Filters — search, reason, channel, warehouse, performer and date range — are server-side;
 * warehouse/performer options are derived from the loaded rows so the page works for warehouse
 * keepers (who can't list users). A client-side CSV export dumps the current page.
 */
@Component({
  selector: 'app-admin-stock-out-log',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, TranslatePipe, PageHeader],
  templateUrl: './stock-out-log.html',
})
export class AdminStockOutLog {
  private readonly service = inject(AdminInventoryService);
  private readonly translate = inject(TranslateService);

  protected readonly term = signal('');
  protected readonly reason = signal<number | null>(null);
  protected readonly channel = signal<number | null>(null);
  protected readonly warehouseId = signal<number | null>(null);
  protected readonly performedById = signal<number | null>(null);
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly page = signal(1);

  protected readonly reasonKeys = REASON_KEYS;
  protected readonly channelKeys = CHANNEL_KEYS;
  protected readonly reasonOptions = Object.entries(REASON_KEYS).map(([v, k]) => ({ value: +v, key: k }));
  protected readonly channelOptions = Object.entries(CHANNEL_KEYS).map(([v, k]) => ({ value: +v, key: k }));

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly query = computed<AdminStockOutQuery>(() => ({
    query: this.term() || undefined,
    reason: this.reason() ?? undefined,
    channel: this.channel() ?? undefined,
    warehouseId: this.warehouseId() ?? undefined,
    performedById: this.performedById() ?? undefined,
    from: this.from() ? `${this.from()}T00:00:00` : undefined,
    to: this.to() ? `${this.to()}T23:59:59` : undefined,
    page: this.page(),
    pageSize: PAGE_SIZE,
  }));

  protected readonly logs = this.service.stockOutLogResource(this.query);

  protected readonly hasMore = computed(
    () => (this.logs.value()?.length ?? 0) === PAGE_SIZE,
  );

  protected readonly hasFilters = computed(
    () =>
      Boolean(this.term()) ||
      this.reason() !== null ||
      this.channel() !== null ||
      this.warehouseId() !== null ||
      this.performedById() !== null ||
      this.from() !== '' ||
      this.to() !== '',
  );

  /** Warehouse options seen on the loaded page. */
  protected readonly warehouses = computed(() => {
    const seen = new Map<number, string>();
    for (const row of this.logs.value() ?? []) {
      seen.set(row.warehouseId, row.warehouseName ?? `#${row.warehouseId}`);
    }
    return [...seen].map(([id, name]) => ({ id, name }));
  });

  /** Performer options seen on the loaded page. */
  protected readonly performers = computed(() => {
    const seen = new Map<number, string>();
    for (const row of this.logs.value() ?? []) {
      if (row.performedById !== null) {
        seen.set(row.performedById, row.performedByName ?? `#${row.performedById}`);
      }
    }
    return [...seen].map(([id, name]) => ({ id, name }));
  });

  protected reasonBadge(reason: number | null): string {
    switch (reason) {
      case 1:
        return 'text-bg-success';
      case 2:
        return 'text-bg-info';
      case 6:
        return 'text-bg-warning';
      case 7:
        return 'text-bg-secondary';
      default:
        return 'text-bg-light';
    }
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => {
      this.term.set(value);
      this.page.set(1);
    }, 300);
  }

  protected setReason(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.reason.set(value === '' ? null : Number(value));
    this.page.set(1);
  }

  protected setChannel(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.channel.set(value === '' ? null : Number(value));
    this.page.set(1);
  }

  protected setWarehouse(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.warehouseId.set(value === '' ? null : Number(value));
    this.page.set(1);
  }

  protected setPerformer(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.performedById.set(value === '' ? null : Number(value));
    this.page.set(1);
  }

  protected setFrom(event: Event): void {
    this.from.set((event.target as HTMLInputElement).value);
    this.page.set(1);
  }

  protected setTo(event: Event): void {
    this.to.set((event.target as HTMLInputElement).value);
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.term.set('');
    this.reason.set(null);
    this.channel.set(null);
    this.warehouseId.set(null);
    this.performedById.set(null);
    this.from.set('');
    this.to.set('');
    this.page.set(1);
  }

  protected prev(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  protected next(): void {
    if (this.hasMore()) {
      this.page.update((p) => p + 1);
    }
  }

  protected exportCsv(): void {
    const rows = this.logs.value() ?? [];
    const header = ['Date', 'Product', 'Quantity', 'Reason', 'Channel', 'Performed by', 'Recipient/Ref', 'Note'];
    const body = rows.map((r) =>
      [
        r.createdOn,
        r.productName ?? '',
        r.quantity,
        r.reason !== null ? this.translate.instant('stock.reason.' + REASON_KEYS[r.reason]) : '',
        r.channel !== null ? this.translate.instant('stock.channel.' + CHANNEL_KEYS[r.channel]) : '',
        r.performedByName ?? '',
        r.recipientOrRef ?? '',
        r.note ?? '',
      ]
        .map(csvCell)
        .join(','),
    );
    const csv = [header.join(','), ...body].join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'stock-out-log.csv';
    link.click();
    URL.revokeObjectURL(url);
  }
}

function csvCell(value: unknown): string {
  const text = value === null || value === undefined ? '' : String(value);
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}
