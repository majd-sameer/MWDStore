import {
  ChangeDetectionStrategy,
  Component,
  inject,
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

/**
 * Payments browser: the gateway list (enable inline, configure on its own page)
 * plus the transaction log. Providers are a fixed set — configuring one happens
 * at `/payments/:id`; there is no create/delete.
 */
@Component({
  selector: 'app-admin-payments',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './payments.html',
})
export class AdminPayments {
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();
  protected readonly payments = this.service.paymentsResource();

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
