import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import {
  AdminInventoryService,
  AdminProductsService,
  SALES_CHANNEL,
  STOCK_OUT_REASON,
  type AdminProductListItem,
  type AdminProductQuery,
  type StockAdjustmentRequest,
  type StockOutRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
import { NgbOffcanvas, type NgbOffcanvasRef } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

const DEFAULT_PAGE_SIZE = 10;

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
 * Stock-out browser: the paged product list (`GET /api/admin/products`) with a
 * debounced name search, and a per-row "Out" action that slides an offcanvas in
 * from the end. The panel loads that product's per-warehouse stock and hosts the
 * stock-out form — pick a warehouse, quantity, a business reason, an optional
 * sales channel (required for Sale), a recipient/reference and a note. Quantity
 * is validated against on-hand both here and server-side.
 */
@Component({
  selector: 'app-admin-stock-out',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    NgSelectModule,
    Icon,
    TranslatePipe,
    PageHeader,
    TableSkeleton,
    TableFooter,
  ],
  templateUrl: './stock-out.html',
})
export class AdminStockOut {
  private readonly service = inject(AdminInventoryService);
  private readonly productsService = inject(AdminProductsService);
  private readonly offcanvas = inject(NgbOffcanvas);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly reasons = REASONS;
  protected readonly channels = CHANNELS;

  // ----- Product list: search + pagination (server-side) -----------------------
  protected readonly term = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly query = computed<AdminProductQuery>(() => ({
    query: this.term() || undefined,
    page: this.page(),
    pageSize: this.pageSize(),
  }));

  protected readonly products = this.productsService.listResource(this.query);
  protected readonly rows = computed(() => this.products.value()?.items ?? []);
  protected readonly total = computed(() => this.products.value()?.total ?? 0);

  /** Products whose thumbnail file 404'd — show the icon tile instead. */
  protected readonly thumbFailed = signal<ReadonlySet<number>>(new Set());

  protected onThumbError(id: number): void {
    this.thumbFailed.update((s) => new Set(s).add(id));
  }

  /** Live search, debounced so we don't hit the API on every keystroke. */
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

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  // ----- Stock-out offcanvas form ----------------------------------------------
  private readonly panel =
    viewChild.required<TemplateRef<unknown>>('stockOutPanel');
  private panelRef: NgbOffcanvasRef | null = null;

  /** Whether the panel is recording a removal ('out') or a receipt ('in'). */
  protected readonly mode = signal<'out' | 'in'>('out');

  /** The product the panel is open for (drives the header + the stock lookup). */
  protected readonly activeProduct = signal<AdminProductListItem | null>(null);
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
    // Receiving stock has no on-hand ceiling and no channel; only removals do.
    if (this.mode() === 'out') {
      if (q > this.onHand()) {
        return 'stockout.err_overstock';
      }
      if (this.channelRequired() && this.channel() === null) {
        return 'stockout.err_channel';
      }
    }
    return null;
  });

  /** Opens the removal ("out") panel for a product. */
  protected openStockOut(product: AdminProductListItem): void {
    this.openPanel(product, 'out');
  }

  /** Opens the receipt ("in") panel for a product. */
  protected openStockIn(product: AdminProductListItem): void {
    this.openPanel(product, 'in');
  }

  /** Opens the offcanvas for a product in the given mode, resetting the form. */
  private openPanel(product: AdminProductListItem, mode: 'out' | 'in'): void {
    this.mode.set(mode);
    this.activeProduct.set(product);
    this.productId.set(product.id);
    this.warehouseId.set(null);
    this.quantity.set(1);
    this.reason.set(STOCK_OUT_REASON.Sale);
    this.channel.set(SALES_CHANNEL.Showroom);
    this.recipient.set('');
    this.note.set('');
    this.stock.reload();
    this.panelRef = this.offcanvas.open(this.panel(), {
      position: 'end',
      panelClass: 'stock-out-panel',
      ariaLabelledBy: 'stock-out-panel-title',
      // Only the Cancel/Save actions close the panel — ignore backdrop clicks
      // and the Esc key so an accidental click can't discard an in-progress form.
      backdrop: 'static',
      keyboard: false,
    });
  }

  protected closePanel(): void {
    this.panelRef?.dismiss();
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

    const quantity = Number(this.quantity());
    const isIn = this.mode() === 'in';

    // Stock-in is a positive warehouse adjustment; stock-out records a reason/channel.
    const request$ = isIn
      ? this.service.adjust({
          productId,
          warehouseId,
          adjustedQuantity: quantity,
          note: this.note() || null,
        } satisfies StockAdjustmentRequest)
      : this.service.stockOut({
          productId,
          warehouseId,
          quantity,
          reason: this.reason(),
          channel: this.channelVisible() ? this.channel() : null,
          recipientOrRef: this.recipient() || null,
          note: this.note() || null,
        } satisfies StockOutRequest);

    const okKey = isIn ? 'inventory.adjusted_ok' : 'stockout.done';
    const failKey = isIn ? 'inventory.adjust_failed' : 'stockout.failed';

    this.saving.set(true);
    void firstValueFrom(request$)
      .then(() => {
        this.toast.success(this.translate.instant(okKey));
        this.panelRef?.close();
        this.products.reload();
      })
      .catch((err: { error?: { error?: string } }) =>
        this.toast.error(err?.error?.error ?? this.translate.instant(failKey)),
      )
      .finally(() => this.saving.set(false));
  }
}
