import { DatePipe } from '@angular/common';
import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  OwlDateTimeModule,
  OwlNativeDateTimeModule,
} from '@danielmoncada/angular-datetime-picker';
import { AdminOrdersService, type AdminOrderQuery } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from 'core';
import { Icon } from 'ui';
import { dayBoundary } from '../../shared/date-range';
import {
  ORDER_STATUS_OPTIONS,
  orderStatusTone,
} from '../../shared/order-status';
import { PageHeader } from '../../shared/page-header';
import { StatusPill } from '../../shared/status-pill';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';
import { FilterDropdown, type FilterOption, type FilterValue } from '../../shared/filter-dropdown';

const DEFAULT_PAGE_SIZE = 10;

/**
 * Order browser over the paged `GET /api/admin/orders` envelope: a multi-select
 * status filter, numbered pagination with total count, and skeleton loading.
 */
@Component({
  selector: 'app-admin-order-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MoneyPipe,
    DatePipe,
    RouterLink,
    FormsModule,
    OwlDateTimeModule,
    OwlNativeDateTimeModule,
    Icon,
    TranslatePipe,
    PageHeader,
    StatusPill,
    TableSkeleton,
    TableFooter,
    FilterDropdown,
  ],
  templateUrl: './order-list.html',
})
export class AdminOrderList {
  private readonly service = inject(AdminOrdersService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly tone = orderStatusTone;

  /** Status filter options, re-labelled when the console language switches. */
  protected readonly statusFilterOptions = computed<FilterOption[]>(() => {
    this.language.lang();
    return ORDER_STATUS_OPTIONS.map((o) => ({
      value: o.value,
      label: this.translate.instant('orders.status_' + o.value),
    }));
  });

  protected readonly statuses = signal<FilterValue[]>([]);
  protected readonly orderNumber = signal<string>('');
  protected readonly from = signal<Date | null>(null);
  protected readonly to = signal<Date | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly query = computed<AdminOrderQuery>(() => {
    const num = Number(this.orderNumber());
    return {
      statuses: this.statuses() as number[],
      orderNumber:
        this.orderNumber() && Number.isFinite(num) && num > 0 ? num : undefined,
      from: dayBoundary(this.from(), false),
      to: dayBoundary(this.to(), true),
      page: this.page(),
      pageSize: this.pageSize(),
    };
  });

  protected readonly orders = this.service.listResource(this.query);

  protected readonly rows = computed(() => this.orders.value()?.items ?? []);
  protected readonly total = computed(() => this.orders.value()?.total ?? 0);

  protected readonly hasFilters = computed(
    () =>
      this.statuses().length > 0 ||
      Boolean(this.orderNumber()) ||
      this.from() !== null ||
      this.to() !== null,
  );

  protected setStatuses(values: FilterValue[]): void {
    this.statuses.set(values);
    this.page.set(1);
  }

  /** Debounced so we don't hit the API on every keystroke of the order number. */
  protected onOrderNumberInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => {
      this.orderNumber.set(value);
      this.page.set(1);
    }, 300);
  }

  protected setFrom(value: Date | null): void {
    this.from.set(value);
    this.page.set(1);
  }

  protected setTo(value: Date | null): void {
    this.to.set(value);
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.statuses.set([]);
    this.orderNumber.set('');
    this.from.set(null);
    this.to.set(null);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }
}
