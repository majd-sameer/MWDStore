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

/** Shape persisted into the Stripe provider's `additionalSettings` JSON. */
interface StripeConfig {
  publicKey: string;
  privateKey: string;
  /** ISO currency Stripe charges in (lower-case, e.g. `jod`). */
  currency: string;
  /** Stripe webhook signing secret (`whsec_…`); optional — the return page settles without it. */
  webhookSecret: string;
}

const STRIPE_PROVIDER_ID = 'Stripe';

/** Tolerantly read a key from parsed JSON regardless of camel/Pascal casing. */
function readKey(source: Record<string, unknown>, ...names: string[]): string {
  for (const name of names) {
    const value = source[name];
    if (typeof value === 'string') {
      return value;
    }
  }
  return '';
}

// Empty defaults — the admin enters live Stripe keys in the UI. Never ship a
// secret key in source (GitHub secret-scanning blocks it, and it shouldn't be
// in the bundle regardless). publicKey is safe to leave blank too.
const STRIPE_DEFAULTS: StripeConfig = {
  publicKey: '',
  privateKey: '',
  currency: 'jod',
  webhookSecret: '',
};

function parseStripeConfig(additionalSettings: string | null): StripeConfig {
  if (!additionalSettings) {
    return { ...STRIPE_DEFAULTS };
  }
  try {
    const raw = JSON.parse(additionalSettings) as Record<string, unknown>;
    return {
      publicKey: readKey(raw, 'publicKey', 'PublicKey'),
      privateKey: readKey(raw, 'privateKey', 'PrivateKey'),
      currency: readKey(raw, 'currency', 'Currency') || 'jod',
      webhookSecret: readKey(raw, 'webhookSecret', 'WebhookSecret'),
    };
  } catch {
    return { publicKey: '', privateKey: '', currency: 'jod', webhookSecret: '' };
  }
}

/**
 * Dedicated, typed config form for the Stripe gateway — cloned from the legacy
 * PaymentStripe module (Public Key + Secret Key). Fields serialize into the
 * provider's `additionalSettings` JSON that the backend already stores, so it
 * slots into the generic provider list at a more specific route than
 * `payments/:id`.
 */
@Component({
  selector: 'app-admin-payment-stripe-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/payments" class="text-decoration-none">← {{ 'payments.title' | translate }}</a>
    </nav>
    <app-page-header
      [title]="'payments.configure_title' | translate: { name: provider()?.name ?? 'Stripe' }"
      [subtitle]="'payments.stripe.subtitle' | translate"
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
                <input type="checkbox" class="form-check-input" id="stripe-enabled"
                  [checked]="enabled()" (change)="enabled.set($any($event.target).checked)" />
                <label class="form-check-label" for="stripe-enabled">{{ 'common.enabled' | translate }}</label>
              </div>

              <div class="mb-3">
                <label class="form-label" for="stripe-public-key">
                  {{ 'payments.stripe.public_key' | translate }}
                </label>
                <input id="stripe-public-key" type="text"
                  class="form-control font-monospace"
                  autocomplete="off" spellcheck="false"
                  [value]="publicKey()" (input)="publicKey.set($any($event.target).value)" />
              </div>

              <div class="mb-3">
                <label class="form-label" for="stripe-secret-key">
                  {{ 'payments.stripe.secret_key' | translate }}
                </label>
                <input id="stripe-secret-key" type="password"
                  class="form-control font-monospace"
                  autocomplete="off" spellcheck="false"
                  [value]="privateKey()" (input)="privateKey.set($any($event.target).value)" />
                <div class="form-text">{{ 'payments.stripe.secret_key_hint' | translate }}</div>
              </div>

              <div class="mb-3">
                <label class="form-label" for="stripe-currency">
                  {{ 'payments.stripe.currency' | translate }}
                </label>
                <input id="stripe-currency" type="text"
                  class="form-control font-monospace" style="max-width: 10rem"
                  autocomplete="off" spellcheck="false" maxlength="3"
                  [value]="currency()" (input)="currency.set($any($event.target).value)" />
                <div class="form-text">{{ 'payments.stripe.currency_hint' | translate }}</div>
              </div>

              <div class="mb-3">
                <label class="form-label" for="stripe-webhook-secret">
                  {{ 'payments.stripe.webhook_secret' | translate }}
                </label>
                <input id="stripe-webhook-secret" type="password"
                  class="form-control font-monospace"
                  autocomplete="off" spellcheck="false"
                  [value]="webhookSecret()" (input)="webhookSecret.set($any($event.target).value)" />
                <div class="form-text">{{ 'payments.stripe.webhook_secret_hint' | translate }}</div>
              </div>

              <div class="form-actions">
                <button type="button" libButton variant="primary"
                  [disabled]="saving() || !publicKey().trim() || !privateKey().trim()"
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
export class AdminPaymentStripeForm {
  private readonly router = inject(Router);
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();
  protected readonly provider = computed(
    () => this.providers.value()?.find((p) => p.id === STRIPE_PROVIDER_ID) ?? null,
  );

  protected readonly enabled = signal(false);
  protected readonly publicKey = signal('');
  protected readonly privateKey = signal('');
  protected readonly currency = signal('jod');
  protected readonly webhookSecret = signal('');
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
      const config = parseStripeConfig(p.additionalSettings);
      this.publicKey.set(config.publicKey);
      this.privateKey.set(config.privateKey);
      this.currency.set(config.currency);
      this.webhookSecret.set(config.webhookSecret);
    });
  }

  protected save(p: AdminPaymentProviderDto): void {
    this.saving.set(true);
    const config: StripeConfig = {
      publicKey: this.publicKey().trim(),
      privateKey: this.privateKey().trim(),
      currency: (this.currency().trim() || 'jod').toLowerCase(),
      webhookSecret: this.webhookSecret().trim(),
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
