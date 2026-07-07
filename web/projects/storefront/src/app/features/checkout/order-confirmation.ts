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
  templateUrl: './order-confirmation.html',
  styleUrl: './order-confirmation.scss',
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
