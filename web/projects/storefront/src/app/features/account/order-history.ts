import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import { OrderService } from 'data-access';
import { TableCards } from 'ui';
import { statusLabel } from './order-status';

@Component({
  selector: 'app-order-history',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, DatePipe, TranslatePipe, TableCards],
  template: `
    <h1 class="h3 mb-4">{{ 'account.history_title' | translate }}</h1>

    @if (orders.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (orders.error()) {
      <div class="alert alert-danger">{{ 'account.orders_error' | translate }}</div>
    } @else if (orders.value(); as list) {
      @if (list.length) {
        <div class="table-responsive">
          <table class="table align-middle" libTableCards>
            <thead>
              <tr>
                <th scope="col">{{ 'account.col_order' | translate }}</th>
                <th scope="col">{{ 'account.col_date' | translate }}</th>
                <th scope="col">{{ 'account.col_status' | translate }}</th>
                <th scope="col" class="text-center">{{ 'account.col_items' | translate }}</th>
                <th scope="col" class="text-end">{{ 'account.col_total' | translate }}</th>
                <th scope="col"></th>
              </tr>
            </thead>
            <tbody>
              @for (order of list; track order.id) {
                <tr>
                  <th scope="row">#{{ order.id }}</th>
                  <td>{{ order.createdOn | date: 'mediumDate' : '' : locale() }}</td>
                  <td>
                    <span class="badge text-bg-secondary">{{
                      statusLabel(order.orderStatus, order.orderStatusName) | translate
                    }}</span>
                  </td>
                  <td class="text-center">{{ order.itemCount }}</td>
                  <td class="text-end tabular-nums">{{ order.orderTotal | money }}</td>
                  <td class="text-end">
                    <a [routerLink]="['/account/orders', order.id]">{{ 'account.col_view' | translate }}</a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <div class="text-center py-5">
          <p class="lead text-body-secondary">{{ 'account.no_orders' | translate }}</p>
          <a class="btn btn-primary" routerLink="/shop">{{ 'account.start_shopping' | translate }}</a>
        </div>
      }
    }
  `,
})
export class OrderHistory {
  private readonly orderService = inject(OrderService);
  private readonly language = inject(LanguageService);
  protected readonly orders = this.orderService.ordersResource();
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));
  protected readonly statusLabel = statusLabel;
}
