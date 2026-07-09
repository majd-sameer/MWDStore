import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminShippingService,
  type AdminShippingProviderDto,
  type AdminTableRateDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

const DEFAULT_PAGE_SIZE = 10;

/**
 * Shipping browser: provider config (enable + free-shipping threshold) plus the
 * table-rates list. Creating and editing a table rate happen on their own page
 * (`/shipping/new`, `/shipping/:id`); providers stay here as config-only.
 *
 * The table-rates endpoint returns the full list, so the search filter and
 * pagination below run client-side over `tableRates.value()`.
 */
@Component({
  selector: 'app-admin-shipping',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton, TableFooter],
  templateUrl: './shipping.html',
})
export class AdminShipping {
  private readonly service = inject(AdminShippingService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();
  protected readonly tableRates = this.service.tableRatesResource();
  protected readonly deletingId = signal<number | null>(null);

  protected readonly search = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  /** Rows matching the search term (provider / country / state / zip). */
  private readonly filtered = computed<AdminTableRateDto[]>(() => {
    const rows = this.tableRates.value() ?? [];
    const term = this.search().trim().toLowerCase();
    if (!term) {
      return rows;
    }
    return rows.filter((r) =>
      [
        r.shippingProviderName,
        r.shippingProviderId,
        r.countryName,
        r.stateOrProvinceName,
        r.zipCode,
      ].some((field) => (field ?? '').toLowerCase().includes(term)),
    );
  });

  protected readonly total = computed(() => this.filtered().length);

  /** The current page of filtered rows; the page is clamped to the last one. */
  protected readonly rows = computed<AdminTableRateDto[]>(() => {
    const size = this.pageSize();
    const lastPage = Math.max(1, Math.ceil(this.total() / size));
    const page = Math.min(this.page(), lastPage);
    return this.filtered().slice((page - 1) * size, page * size);
  });

  protected setSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

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
