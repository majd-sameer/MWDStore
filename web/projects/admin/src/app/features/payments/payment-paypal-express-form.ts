import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  AdminPaymentsService,
  type AdminPaymentProviderDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/** Shape persisted into the PayPal Express provider's `additionalSettings` JSON. */
interface PaypalExpressConfig {
  isSandbox: boolean;
  clientId: string;
  clientSecret: string;
  paymentFee: number;
}

const PAYPAL_EXPRESS_PROVIDER_ID = 'PaypalExpress';

/** Tolerantly read a string from parsed JSON regardless of camel/Pascal casing. */
function readString(source: Record<string, unknown>, ...names: string[]): string {
  for (const name of names) {
    const value = source[name];
    if (typeof value === 'string') {
      return value;
    }
  }
  return '';
}

function parsePaypalExpressConfig(
  additionalSettings: string | null,
): PaypalExpressConfig {
  // Legacy seed default: { "IsSandbox": true, "ClientId": "", "ClientSecret": "" }.
  const fallback: PaypalExpressConfig = {
    isSandbox: true,
    clientId: '',
    clientSecret: '',
    paymentFee: 0,
  };
  if (!additionalSettings) {
    return fallback;
  }
  try {
    const raw = JSON.parse(additionalSettings) as Record<string, unknown>;
    const sandbox = raw['isSandbox'] ?? raw['IsSandbox'];
    const fee = raw['paymentFee'] ?? raw['PaymentFee'];
    return {
      isSandbox: sandbox === true,
      clientId: readString(raw, 'clientId', 'ClientId'),
      clientSecret: readString(raw, 'clientSecret', 'ClientSecret'),
      paymentFee: typeof fee === 'number' ? fee : Number(fee) || 0,
    };
  } catch {
    return fallback;
  }
}

/**
 * Dedicated, typed config form for PayPal Express — cloned from the legacy
 * PaymentPaypalExpress module (Sandbox toggle, Client ID, Client Secret,
 * Payment Fee %). Fields serialize into the provider's `additionalSettings`
 * JSON that the backend already stores, slotting into the generic provider
 * list at a more specific route than `payments/:id`.
 */
@Component({
  selector: 'app-admin-payment-paypal-express-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/payments" class="text-decoration-none">← {{ 'payments.title' | translate }}</a>
    </nav>
    <app-page-header
      [title]="'payments.configure_title' | translate: { name: provider()?.name ?? 'PayPal Express' }"
      [subtitle]="'payments.paypal.subtitle' | translate"
    />

    @if (providers.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (providers.error() || !provider()) {
      <div class="alert alert-danger">{{ 'payments.load_one_failed' | translate }}</div>
    } @else if (provider(); as p) {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <div class="d-flex align-items-center gap-3 mb-3">
                <span class="fw-semibold fs-5">{{ p.name }}</span>
                <code class="small text-body-secondary">{{ p.id }}</code>
              </div>

              <div class="form-check form-switch mb-3">
                <input type="checkbox" class="form-check-input" id="paypal-enabled"
                  [checked]="enabled()" (change)="enabled.set($any($event.target).checked)" />
                <label class="form-check-label" for="paypal-enabled">{{ 'common.enabled' | translate }}</label>
              </div>

              <div class="form-check form-switch mb-3">
                <input type="checkbox" class="form-check-input" id="paypal-sandbox"
                  [checked]="isSandbox()" (change)="isSandbox.set($any($event.target).checked)" />
                <label class="form-check-label" for="paypal-sandbox">{{ 'payments.paypal.sandbox' | translate }}</label>
              </div>

              <div class="mb-3">
                <label class="form-label" for="paypal-client-id">
                  {{ 'payments.paypal.client_id' | translate }}
                </label>
                <input id="paypal-client-id" type="text"
                  class="form-control font-monospace"
                  autocomplete="off" spellcheck="false"
                  [value]="clientId()" (input)="clientId.set($any($event.target).value)" />
              </div>

              <div class="mb-3">
                <label class="form-label" for="paypal-client-secret">
                  {{ 'payments.paypal.client_secret' | translate }}
                </label>
                <input id="paypal-client-secret" type="password"
                  class="form-control font-monospace"
                  autocomplete="off" spellcheck="false"
                  [value]="clientSecret()" (input)="clientSecret.set($any($event.target).value)" />
                <div class="form-text">{{ 'payments.paypal.client_secret_hint' | translate }}</div>
              </div>

              <div class="mb-3">
                <label class="form-label" for="paypal-payment-fee">
                  {{ 'payments.paypal.payment_fee' | translate }}
                </label>
                <input id="paypal-payment-fee" type="number" min="0" step="0.01"
                  class="form-control"
                  [value]="paymentFee()" (input)="paymentFee.set($any($event.target).value)" />
              </div>

              <div class="form-actions">
                <button type="button" libButton variant="primary"
                  [disabled]="saving() || !clientId().trim() || !clientSecret().trim()"
                  (click)="save(p)">
                  {{ (saving() ? 'common.saving' : 'common.save_changes') | translate }}
                </button>
                <a routerLink="/payments" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
              </div>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminPaymentPaypalExpressForm {
  private readonly router = inject(Router);
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();
  protected readonly provider = computed(
    () =>
      this.providers.value()?.find((p) => p.id === PAYPAL_EXPRESS_PROVIDER_ID) ??
      null,
  );

  protected readonly enabled = signal(false);
  protected readonly isSandbox = signal(false);
  protected readonly clientId = signal('');
  protected readonly clientSecret = signal('');
  protected readonly paymentFee = signal<number | string>(0);
  protected readonly saving = signal(false);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.seeded) {
        return;
      }
      const p = this.provider();
      if (!p) {
        return;
      }
      this.seeded = true;
      this.enabled.set(p.isEnabled);
      const config = parsePaypalExpressConfig(p.additionalSettings);
      this.isSandbox.set(config.isSandbox);
      this.clientId.set(config.clientId);
      this.clientSecret.set(config.clientSecret);
      this.paymentFee.set(config.paymentFee);
    });
  }

  protected save(p: AdminPaymentProviderDto): void {
    this.saving.set(true);
    const config: PaypalExpressConfig = {
      isSandbox: this.isSandbox(),
      clientId: this.clientId().trim(),
      clientSecret: this.clientSecret().trim(),
      paymentFee: Number(this.paymentFee()) || 0,
    };
    this.service
      .updateProvider(p.id, {
        name: p.name ?? p.id,
        isEnabled: this.enabled(),
        additionalSettings: JSON.stringify(config),
      })
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('payments.saved_ok'));
          this.saving.set(false);
          void this.router.navigate(['/payments']);
        },
        error: () => {
          this.toast.error(this.translate.instant('payments.save_failed'));
          this.saving.set(false);
        },
      });
  }
}
