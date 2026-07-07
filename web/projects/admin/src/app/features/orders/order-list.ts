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
import { AdminOrdersService, type AdminOrderQuery } from 'data-access';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon } from 'ui';
import {
  ORDER_STATUS_OPTIONS,
  orderStatusBadge,
} from '../../shared/order-status';
import { PageHeader } from '../../shared/page-header';

const PAGE_SIZE = 25;

/** Order browser: one-click status chips + pagination over `GET /api/admin/orders`. */
@Component({
  selector: 'app-admin-order-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './order-list.html',
})
export class AdminOrderList {
  private readonly service = inject(AdminOrdersService);

  protected readonly statusOptions = ORDER_STATUS_OPTIONS;
  protected readonly badge = orderStatusBadge;

  protected readonly status = signal<number | null>(null);
  protected readonly page = signal(1);

  private readonly query = computed<AdminOrderQuery>(() => ({
    status: this.status() ?? undefined,
    page: this.page(),
    pageSize: PAGE_SIZE,
  }));

  protected readonly orders = this.service.listResource(this.query);

  protected readonly hasMore = computed(
    () => (this.orders.value()?.length ?? 0) === PAGE_SIZE,
  );

  protected setStatus(value: number | null): void {
    this.status.set(value);
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
}
