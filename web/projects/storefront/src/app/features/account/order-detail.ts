import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { OrderService } from 'data-access';
import { Breadcrumb, type BreadcrumbItem } from 'ui';
import { OrderDetailView } from '../../shared/order-detail-view';

@Component({
  selector: 'app-order-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Breadcrumb, OrderDetailView],
  template: `
    <lib-breadcrumb [items]="crumbs()" />

    @if (order.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (order.error()) {
      <div class="alert alert-danger">{{ 'confirmation.load_error' | translate }}</div>
    } @else {
      <div class="card">
        <div class="card-body">
          <app-order-detail-view [order]="order.value()" />
        </div>
      </div>
    }
  `,
})
export class OrderDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly orders = inject(OrderService);
  private readonly translate = inject(TranslateService);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly orderId = computed(() => Number(this.params().get('id')));

  protected readonly order = this.orders.orderResource(this.orderId);

  protected readonly crumbs = computed<BreadcrumbItem[]>(() => [
    { label: this.translate.instant('account.title'), link: '/account' },
    { label: this.translate.instant('account.orders'), link: '/account/orders' },
    { label: this.translate.instant('account.order_no', { id: this.orderId() }) },
  ]);
}
