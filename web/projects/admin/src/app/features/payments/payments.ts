import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MoneyPipe } from 'core';
import { RouterLink } from '@angular/router';
import {
  AdminPaymentsService,
  type AdminPaymentProviderDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

const DEFAULT_PAGE_SIZE = 25;

/**
 * Payments browser: the gateway list (enable inline, configure on its own page)
 * plus the transaction log. Providers are a fixed set — configuring one happens
 * at `/payments/:id`; there is no create/delete.
 */
@Component({
  selector: 'app-admin-payments',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MoneyPipe,
    DatePipe,
    RouterLink,
    Icon,
    TranslatePipe,
    PageHeader,
    TableSkeleton,
    TableFooter,
  ],
  templateUrl: './payments.html',
})
export class AdminPayments {
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();

  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  private readonly paymentsQuery = computed(() => ({
    page: this.page(),
    pageSize: this.pageSize(),
  }));

  protected readonly payments = this.service.paymentsResource(this.paymentsQuery);

  protected readonly paymentRows = computed(() => this.payments.value()?.items ?? []);
  protected readonly paymentsTotal = computed(() => this.payments.value()?.total ?? 0);

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  protected toggle(p: AdminPaymentProviderDto, isEnabled: boolean): void {
    this.service
      .updateProvider(p.id, {
        name: p.name ?? p.id,
        isEnabled,
        additionalSettings: p.additionalSettings,
      })
      .subscribe({
        next: () => {
          this.toast.success(
            this.translate.instant(
              isEnabled ? 'payments.provider_enabled' : 'payments.provider_disabled',
              { name: p.name },
            ),
          );
          this.providers.reload();
        },
        error: () => this.toast.error(this.translate.instant('payments.provider_update_failed')),
      });
  }
}
