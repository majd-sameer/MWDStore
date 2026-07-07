import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminShippingService,
  type AdminShippingProviderDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Shipping browser: provider config (enable + free-shipping threshold) plus the
 * table-rates list. Creating and editing a table rate happen on their own page
 * (`/shipping/new`, `/shipping/:id`); providers stay here as config-only.
 */
@Component({
  selector: 'app-admin-shipping',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './shipping.html',
})
export class AdminShipping {
  private readonly service = inject(AdminShippingService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();
  protected readonly tableRates = this.service.tableRatesResource();
  protected readonly deletingId = signal<number | null>(null);

  protected toggleProvider(p: AdminShippingProviderDto, isEnabled: boolean): void {
    this.service
      .updateProvider(p.id, {
        name: p.name ?? p.id,
        isEnabled,
        freeShippingMinimumOrderAmount: p.freeShippingMinimumOrderAmount,
      })
      .subscribe({
        next: () => {
          this.toast.success(
            this.translate.instant(
              isEnabled ? 'shipping.provider_enabled' : 'shipping.provider_disabled',
              { name: p.name },
            ),
          );
          this.providers.reload();
        },
        error: () => this.toast.error(this.translate.instant('shipping.provider_update_failed')),
      });
  }

  protected setFreeMinimum(p: AdminShippingProviderDto, minimum: number): void {
    this.service
      .updateProvider(p.id, {
        name: p.name ?? p.id,
        isEnabled: p.isEnabled,
        freeShippingMinimumOrderAmount: minimum,
      })
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('shipping.free_min_updated'));
          this.providers.reload();
        },
        error: () => this.toast.error(this.translate.instant('shipping.provider_update_failed')),
      });
  }

  protected removeRate(id: number): void {
    if (!confirm(this.translate.instant('shipping.confirm_delete_rate'))) {
      return;
    }
    this.deletingId.set(id);
    this.service.deleteTableRate(id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.tableRates.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('shipping.rate_delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
