import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import type { OrderAddressDto, OrderDetailDto } from 'data-access';
import { statusLabel } from '../features/account/order-status';

/** Presentational rendering of a single order, shared by confirmation + account. */
@Component({
  selector: 'app-order-detail-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, TranslatePipe],
  templateUrl: './order-detail-view.html',
})
export class OrderDetailView {
  private readonly language = inject(LanguageService);
  readonly order = input.required<OrderDetailDto | undefined>();

  /** Active locale for date formatting; prices stay Western (en-US). */
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));

  /** Localized status label (i18n key by code, falling back to the raw backend name). */
  protected readonly statusLabel = statusLabel;

  protected formatAddress(address: OrderAddressDto): string {
    return [
      address.contactName,
      address.addressLine1,
      address.addressLine2,
      address.city,
      address.zipCode,
      address.countryId,
    ]
      .filter(Boolean)
      .join(', ');
  }
}
