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
  template: `
    <app-page-header
      [title]="'orders.title' | translate"
      [subtitle]="'orders.subtitle' | translate"
    />

    <div class="card border-0 shadow-sm">
      <div class="card-body">
        <div class="filter-chips mb-3" role="group" [attr.aria-label]="'common.status' | translate">
          <button
            type="button"
            class="filter-chip"
            [class.active]="status() === null"
            (click)="setStatus(null)"
          >
            {{ 'common.all' | translate }}
          </button>
          @for (opt of statusOptions; track opt.value) {
            <button
              type="button"
              class="filter-chip"
              [class.active]="status() === opt.value"
              (click)="setStatus(opt.value)"
            >
              {{ 'orders.status_' + opt.value | translate }}
            </button>
          }
        </div>

        @if (orders.isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (orders.error()) {
          <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
        } @else if (orders.value(); as rows) {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th scope="col">{{ 'dashboard.col_order' | translate }}</th>
                  <th scope="col">{{ 'dashboard.col_placed' | translate }}</th>
                  <th scope="col">{{ 'common.status' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'dashboard.col_items' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'dashboard.col_total' | translate }}</th>
                  <th scope="col"></th>
                </tr>
              </thead>
              <tbody>
                @for (o of rows; track o.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/orders', o.id]" class="text-decoration-none fw-medium">
                        #{{ o.id }}
                      </a>
                    </td>
                    <td>{{ o.createdOn | date: 'medium' }}</td>
                    <td>
                      <span class="badge" [class]="badge(o.orderStatus)">{{ o.orderStatusName }}</span>
                    </td>
                    <td class="text-end tabular-nums">{{ o.itemCount }}</td>
                    <td class="text-end tabular-nums">{{ o.orderTotal | money }}</td>
                    <td class="text-end">
                      <a [routerLink]="['/orders', o.id]" class="action-btn" [title]="'common.view' | translate">
                        <lib-icon name="eye" [size]="15" [label]="'common.view' | translate" />
                      </a>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="bag" [size]="26" /></span>
                        <div class="empty-title">
                          {{ (status() === null ? 'orders.empty_all' : 'orders.empty_status') | translate }}
                        </div>
                        @if (status() !== null) {
                          <button type="button" class="btn btn-link btn-sm" (click)="setStatus(null)">
                            {{ 'orders.show_all' | translate }}
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          @if (rows.length || page() > 1) {
            <div class="list-pager">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-1"
                [disabled]="page() === 1"
                (click)="prev()"
              >
                <lib-icon name="chevStart" [size]="15" />
                {{ 'common.previous' | translate }}
              </button>
              <span class="page-chip">{{ 'common.page_info' | translate: { page: page(), count: rows.length } }}</span>
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-1"
                [disabled]="!hasMore()"
                (click)="next()"
              >
                {{ 'common.next' | translate }}
                <lib-icon name="chevEnd" [size]="15" />
              </button>
            </div>
          }
        }
      </div>
    </div>
  `,
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
