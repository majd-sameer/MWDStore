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

/** Shape persisted into the MadfoatCom provider's `additionalSettings` JSON. */
interface MadfoatcomConfig {
  /** PayTabs merchant profile id (numeric, from Dashboard → Developers → Getting Started). */
  profileId: string;
  /** Profile server key — authenticates API calls and keys the callback/return HMAC. Server-side only. */
  serverKey: string;
  /** Profile client key. Unused by the hosted-page flow; kept for future browser-side integrations. */
  clientKey: string;
  /** PayTabs region code, which selects the API domain. */
  region: string;
  /** Three-letter ISO currency PayTabs charges in. Must be enabled on the profile. */
  currency: string;
  /** True when the profile is a PayTabs demo/test profile (test cards only). */
  isSandbox: boolean;
  paymentFee: number | string;
}

const MADFOATCOM_PROVIDER_ID = 'MadfoatCom';

/**
 * PayTabs regions. A merchant account lives in exactly one, and calling the wrong domain fails as
 * an authentication error rather than a routing one — hence an explicit choice, not a guess.
 */
const REGIONS = [
  { code: 'MADFOAT', label: 'Madfoat white-label (madfoat-secure.paytabs.com)' },
  { code: 'ARE', label: 'United Arab Emirates (secure.paytabs.com)' },
  { code: 'SAU', label: 'Saudi Arabia (secure.paytabs.sa)' },
  { code: 'EGY', label: 'Egypt (secure-egypt.paytabs.com)' },
  { code: 'JOR', label: 'Jordan (secure-jordan.paytabs.com)' },
  { code: 'OMN', label: 'Oman (secure-oman.paytabs.com)' },
  { code: 'KWT', label: 'Kuwait (secure-kuwait.paytabs.com)' },
  { code: 'GLOBAL', label: 'Global (secure-global.paytabs.com)' },
] as const;

/** Tolerantly read a key from parsed JSON regardless of camel/Pascal casing or numeric typing. */
function readKey(source: Record<string, unknown>, ...names: string[]): string {
  for (const name of names) {
    const value = source[name];
    if (typeof value === 'string') {
      return value;
    }
    // profileId is legitimately a number in hand-written or seeded settings JSON.
    if (typeof value === 'number') {
      return String(value);
    }
  }
  return '';
}

// Empty credential defaults — the admin pastes real PayTabs keys in the UI. Never ship a server key
// in source: it is the HMAC key for callback verification as well as the API credential.
const MADFOATCOM_DEFAULTS: MadfoatcomConfig = {
  profileId: '',
  serverKey: '',
  clientKey: '',
  region: 'MADFOAT',
  currency: 'JOD',
  isSandbox: true,
  paymentFee: 0,
};

function parseConfig(additionalSettings: string | null): MadfoatcomConfig {
  if (!additionalSettings) {
    return { ...MADFOATCOM_DEFAULTS };
  }
  try {
    const raw = JSON.parse(additionalSettings) as Record<string, unknown>;
    const fee = raw['paymentFee'] ?? raw['PaymentFee'];
    return {
      profileId: readKey(raw, 'profileId', 'ProfileId', 'profile_id'),
      serverKey: readKey(raw, 'serverKey', 'ServerKey', 'server_key'),
      clientKey: readKey(raw, 'clientKey', 'ClientKey', 'client_key'),
      region: readKey(raw, 'region', 'Region') || 'MADFOAT',
      currency: (readKey(raw, 'currency', 'Currency') || 'JOD').toUpperCase(),
      isSandbox: (raw['isSandbox'] ?? raw['IsSandbox'] ?? true) !== false,
      paymentFee: typeof fee === 'number' || typeof fee === 'string' ? fee : 0,
    };
  } catch {
    return { ...MADFOATCOM_DEFAULTS };
  }
}

/**
 * Config form for the MadfoatCom gateway, which runs on PayTabs' Hosted Payment Page. Fields
 * serialize into the provider's `additionalSettings` JSON the backend already stores, so it slots
 * into the generic provider list at a more specific route than `payments/:id`.
 */
@Component({
  selector: 'app-admin-payment-madfoatcom-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './payment-madfoatcom-form.html',
})
export class AdminPaymentMadfoatcomForm {
  private readonly router = inject(Router);
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly regions = REGIONS;

  protected readonly providers = this.service.providersResource();
  protected readonly provider = computed(
    () => this.providers.value()?.find((p) => p.id === MADFOATCOM_PROVIDER_ID) ?? null,
  );

  protected readonly enabled = signal(false);
  protected readonly profileId = signal('');
  protected readonly serverKey = signal('');
  protected readonly clientKey = signal('');
  protected readonly region = signal('MADFOAT');
  protected readonly currency = signal('JOD');
  protected readonly isSandbox = signal(true);
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
      const config = parseConfig(p.additionalSettings);
      this.profileId.set(config.profileId);
      this.serverKey.set(config.serverKey);
      this.clientKey.set(config.clientKey);
      this.region.set(config.region);
      this.currency.set(config.currency);
      this.isSandbox.set(config.isSandbox);
      this.paymentFee.set(config.paymentFee);
    });
  }

  protected save(p: AdminPaymentProviderDto): void {
    this.saving.set(true);
    const config: MadfoatcomConfig = {
      profileId: this.profileId().trim(),
      serverKey: this.serverKey().trim(),
      clientKey: this.clientKey().trim(),
      region: this.region().trim() || 'MADFOAT',
      currency: (this.currency().trim() || 'JOD').toUpperCase(),
      isSandbox: this.isSandbox(),
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
