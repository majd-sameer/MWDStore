import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminSystemService, type AdminCountryDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * Countries browser: a full-width list with inline shipping/billing toggles.
 * Creating and editing a country (and its states) happen on their own page
 * (`/locations/new`, `/locations/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-locations',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './locations.html',
})
export class AdminLocations {
  private readonly service = inject(AdminSystemService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly countries = this.service.countriesResource();
  protected readonly deletingId = signal<string | null>(null);

  protected patchCountry(c: AdminCountryDto, patch: Partial<AdminCountryDto>): void {
    this.service
      .updateCountry(c.id, {
        name: c.name ?? c.id,
        code3: c.code3,
        isBillingEnabled: patch.isBillingEnabled ?? c.isBillingEnabled,
        isShippingEnabled: patch.isShippingEnabled ?? c.isShippingEnabled,
        isCityEnabled: c.isCityEnabled,
        isZipCodeEnabled: c.isZipCodeEnabled,
        isDistrictEnabled: c.isDistrictEnabled,
      })
      .subscribe({
        next: () => this.countries.reload(),
        error: () => this.toast.error(this.translate.instant('countries.update_failed')),
      });
  }

  protected removeCountry(c: AdminCountryDto): void {
    if (!confirm(this.translate.instant('countries.confirm_delete', { name: c.name ?? c.id }))) {
      return;
    }
    this.deletingId.set(c.id);
    this.service.deleteCountry(c.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.countries.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('countries.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
