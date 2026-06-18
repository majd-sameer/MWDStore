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

/**
 * Countries browser: a full-width list with inline shipping/billing toggles.
 * Creating and editing a country (and its states) happen on their own page
 * (`/locations/new`, `/locations/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-locations',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'countries.title' | translate"
      [subtitle]="'countries.subtitle' | translate"
    >
      <a routerLink="/locations/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'countries.new' | translate }}
      </a>
    </app-page-header>

    <div class="card border-0 shadow-sm">
      <div class="card-body">
        @if (countries.isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (countries.error()) {
          <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
        } @else if (countries.value(); as rows) {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th>{{ 'countries.col_code' | translate }}</th>
                  <th>{{ 'common.name' | translate }}</th>
                  <th class="text-center">{{ 'nav.shipping' | translate }}</th>
                  <th class="text-center">{{ 'countries.col_billing' | translate }}</th>
                  <th class="text-end">{{ 'countries.col_states' | translate }}</th>
                  <th class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (c of rows; track c.id) {
                  <tr>
                    <td class="font-monospace">{{ c.id }}</td>
                    <td>
                      <a [routerLink]="['/locations', c.id]" class="text-decoration-none fw-medium">{{ c.name }}</a>
                    </td>
                    <td class="text-center">
                      <input type="checkbox" class="form-check-input" [checked]="c.isShippingEnabled"
                        [attr.aria-label]="'countries.shipping_enabled' | translate"
                        (change)="patchCountry(c, { isShippingEnabled: $any($event.target).checked })" />
                    </td>
                    <td class="text-center">
                      <input type="checkbox" class="form-check-input" [checked]="c.isBillingEnabled"
                        [attr.aria-label]="'countries.billing_enabled' | translate"
                        (change)="patchCountry(c, { isBillingEnabled: $any($event.target).checked })" />
                    </td>
                    <td class="text-end">{{ c.statesCount }}</td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/locations', c.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === c.id"
                          (click)="removeCountry(c)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'countries.empty' | translate }}</div>
                        <a routerLink="/locations/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'countries.create_first' | translate }}
                        </a>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
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
