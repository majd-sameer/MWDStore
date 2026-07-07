import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import { OrderService } from 'data-access';
import { statusLabel } from './order-status';

@Component({
  selector: 'app-order-history',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, DatePipe, TranslatePipe],
  templateUrl: './order-history.html',
})
export class OrderHistory {
  private readonly orderService = inject(OrderService);
  private readonly language = inject(LanguageService);
  protected readonly orders = this.orderService.ordersResource();
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));
  protected readonly statusLabel = statusLabel;
}
