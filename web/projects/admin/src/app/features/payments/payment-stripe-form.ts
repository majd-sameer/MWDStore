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
  templateUrl: './payment-stripe-form.html',
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
