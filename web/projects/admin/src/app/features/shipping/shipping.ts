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
  template: `
    <app-page-header
      [title]="'shipping.title' | translate"
      [subtitle]="'shipping.subtitle' | translate"
    >
      <a routerLink="/shipping/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'shipping.new' | translate }}
      </a>
    </app-page-header>

    <div class="card border-0 shadow-sm mb-4">
      <div class="card-header bg-body fw-semibold">{{ 'shipping.providers_title' | translate }}</div>
      <div class="card-body">
        @if (providers.isLoading()) {
          <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else {
          <table class="table align-middle mb-0">
            <thead>
              <tr>
                <th>{{ 'shipping.col_provider' | translate }}</th>
                <th>{{ 'common.enabled' | translate }}</th>
                <th>{{ 'shipping.col_free_min' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (p of providers.value() ?? []; track p.id) {
                <tr>
                  <td class="fw-medium">{{ p.name }}</td>
                  <td>
                    <div class="form-check form-switch">
                      <input type="checkbox" class="form-check-input" id="prov-{{ p.id }}"
                        [checked]="p.isEnabled"
                        (change)="toggleProvider(p, $any($event.target).checked)" />
                      <label class="form-check-label visually-hidden" for="prov-{{ p.id }}">
                        {{ 'shipping.enable_label' | translate: { name: p.name } }}
                      </label>
                    </div>
                  </td>
                  <td>
                    @if (p.id === 'Free') {
                      <input type="number" step="0.01" class="form-control form-control-sm w-auto"
                        [value]="p.freeShippingMinimumOrderAmount ?? 0"
                        (change)="setFreeMinimum(p, $any($event.target).valueAsNumber || 0)" />
                    } @else {
                      <span class="text-body-secondary small">—</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    </div>

    <div class="card border-0 shadow-sm">
      <div class="card-header bg-body fw-semibold">{{ 'shipping.rates_title' | translate }}</div>
      <div class="card-body">
        @if (tableRates.isLoading()) {
          <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (tableRates.error()) {
          <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
        } @else if (tableRates.value(); as rows) {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th>{{ 'shipping.col_provider' | translate }}</th>
                  <th>{{ 'common.country' | translate }}</th>
                  <th>{{ 'common.state' | translate }}</th>
                  <th>{{ 'common.zip' | translate }}</th>
                  <th class="text-end">{{ 'shipping.col_min_subtotal' | translate }}</th>
                  <th class="text-end">{{ 'shipping.col_price' | translate }}</th>
                  <th class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (r of rows; track r.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/shipping', r.id]" class="text-decoration-none fw-medium">
                        {{ r.shippingProviderName ?? r.shippingProviderId ?? ('common.any' | translate) }}
                      </a>
                    </td>
                    <td>{{ r.countryName ?? ('common.any' | translate) }}</td>
                    <td>{{ r.stateOrProvinceName ?? ('common.any' | translate) }}</td>
                    <td>{{ r.zipCode ?? ('common.any' | translate) }}</td>
                    <td class="text-end">{{ r.minOrderSubtotal }}</td>
                    <td class="text-end">{{ r.shippingPrice }}</td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/shipping', r.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === r.id"
                          (click)="removeRate(r.id)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'shipping.empty' | translate }}</div>
                        <div class="small">{{ 'shipping.empty_hint' | translate }}</div>
                        <a routerLink="/shipping/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'shipping.create_first' | translate }}
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
