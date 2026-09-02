import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import type { OrderSummaryDto } from 'data-access';
import { Icon, Tag } from 'ui';
import { RetryPayment } from '../../shared/retry-payment';
import { statusLabel } from './order-status';
import { TrackBar } from './track-bar';

/**
 * Order summary card for the account page: order number, date, status tag,
 * a TrackBar timeline, item count + total and a link into the order detail.
 * The status label comes from the API; the timeline derives from the code.
 */
@Component({
  selector: 'app-order-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, DatePipe, TranslatePipe, Icon, Tag, TrackBar, RetryPayment],
  host: { class: 'order-card' },
  templateUrl: './order-card.html',
  styleUrl: './order-card.scss',
})
export class OrderCard {
  private readonly language = inject(LanguageService);
  readonly order = input.required<OrderSummaryDto>();

  /** Active locale for date formatting; prices stay Western (en-US). */
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));
  protected readonly statusLabel = statusLabel;
}
