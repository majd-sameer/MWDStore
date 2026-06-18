import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { OrderService } from 'data-access';
import { Button, Icon } from 'ui';
import { OrderDetailView } from '../../shared/order-detail-view';

@Component({
  selector: 'app-order-confirmation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Button, Icon, OrderDetailView],
  template: `
    <div class="confirm-hero">
      <span class="confirm-check"><lib-icon name="check" [size]="34" /></span>
      <h1 class="confirm-title">{{ 'confirmation.title' | translate }}</h1>
      <p class="confirm-sub">{{ 'confirmation.thanks' | translate }}</p>
    </div>

    @if (order.isLoading()) {
      <div class="state">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (order.error()) {
      <div class="alert alert-warning">{{ 'confirmation.load_error' | translate }}</div>
    } @else {
      <div class="confirm-card">
        <app-order-detail-view [order]="order.value()" />
      </div>
    }

    <div class="confirm-ctas">
      <a libButton variant="secondary" [outline]="true" routerLink="/account/orders">
        {{ 'confirmation.view_orders' | translate }}
      </a>
      <a libButton variant="dark" routerLink="/shop">
        {{ 'confirmation.continue' | translate }}
      </a>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .confirm-hero {
      text-align: center;
      padding-block: 1.5rem 2rem;
    }
    .confirm-check {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 64px;
      block-size: 64px;
      border-radius: 50%;
      background: color-mix(in srgb, #2e9e5b 16%, transparent);
      color: #2e9e5b;
      margin-block-end: 1rem;
    }
    .confirm-title {
      font-weight: 700;
      font-size: clamp(1.8rem, 4vw, 2.6rem);
      letter-spacing: -0.02em;
      margin: 0;
    }
    .confirm-sub {
      color: var(--ink-2);
      margin-block-start: 0.5rem;
    }
    .confirm-card {
      max-inline-size: 760px;
      margin-inline: auto;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 1.75rem;
    }
    .state {
      text-align: center;
      padding-block: 3rem;
    }
    .confirm-ctas {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      justify-content: center;
      margin-block-start: 2rem;
    }
  `,
})
export class OrderConfirmation {
  private readonly route = inject(ActivatedRoute);
  private readonly orders = inject(OrderService);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly orderId = computed(() => Number(this.params().get('id')));

  protected readonly order = this.orders.orderResource(this.orderId);
}
