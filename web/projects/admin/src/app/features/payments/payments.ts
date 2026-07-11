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
import { Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Payments browser: the gateway list (enable inline, configure on its own page)
 * plus the transaction log. Providers are a fixed set — configuring one happens
 * at `/payments/:id`; there is no create/delete.
 */
@Component({
  selector: 'app-admin-payments',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, RouterLink, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'payments.title' | translate"
      [subtitle]="'payments.subtitle' | translate"
    />

    <div class="card border-0 shadow-sm mb-4">
      <div class="card-header bg-body fw-semibold">{{ 'payments.providers_title' | translate }}</div>
      <div class="card-body">
        @if (providers.isLoading()) {
          <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (providers.value(); as rows) {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0" libTableCards>
              <thead>
                <tr>
                  <th>{{ 'payments.col_provider' | translate }}</th>
                  <th>{{ 'payments.col_gateway' | translate }}</th>
                  <th>{{ 'common.enabled' | translate }}</th>
                  <th class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (p of rows; track p.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/payments', p.id]" class="text-decoration-none fw-medium">{{ p.name }}</a>
                    </td>
                    <td><code class="small text-body-secondary">{{ p.id }}</code></td>
                    <td>
                      <div class="form-check form-switch">
                        <input type="checkbox" class="form-check-input" id="pay-prov-{{ p.id }}"
                          [checked]="p.isEnabled"
                          (change)="toggle(p, $any($event.target).checked)" />
                        <label class="form-check-label visually-hidden" for="pay-prov-{{ p.id }}">
                          {{ 'payments.enable_label' | translate: { name: p.name } }}
                        </label>
                      </div>
                    </td>
                    <td class="text-end">
                      <a [routerLink]="['/payments', p.id]" class="action-btn" [title]="'payments.configure' | translate">
                        <lib-icon name="pencil" [size]="15" [label]="'payments.configure' | translate" />
                      </a>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>

    <div class="card border-0 shadow-sm">
      <div class="card-header bg-body fw-semibold">{{ 'payments.transactions_title' | translate }}</div>
      <div class="card-body">
        <div class="table-responsive">
        <table class="table table-sm table-hover align-middle mb-0" libTableCards>
          <thead>
            <tr>
              <th>#</th>
              <th>{{ 'dashboard.col_order' | translate }}</th>
              <th>{{ 'payments.col_method' | translate }}</th>
              <th class="text-end">{{ 'payments.col_amount' | translate }}</th>
              <th class="text-end">{{ 'payments.col_fee' | translate }}</th>
              <th>{{ 'payments.col_ref' | translate }}</th>
              <th>{{ 'common.when' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (t of payments.value() ?? []; track t.id) {
              <tr>
                <td>{{ t.id }}</td>
                <td>#{{ t.orderId }}</td>
                <td>{{ t.paymentMethod ?? '—' }}</td>
                <td class="text-end">{{ t.amount | money }}</td>
                <td class="text-end">{{ t.paymentFee | money }}</td>
                <td class="small">{{ t.gatewayTransactionId ?? '—' }}</td>
                <td class="small">{{ t.createdOn | date: 'medium' }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="text-center text-body-secondary py-4">
                  {{ 'payments.no_payments' | translate }}
                </td>
              </tr>
            }
          </tbody>
        </table>
        </div>
      </div>
    </div>
  `,
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
