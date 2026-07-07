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

/** Shape persisted into the MEPS provider's `additionalSettings` JSON. */
interface MepsConfig {
  isSandbox: boolean;
  merchantId: string;
  terminalId: string;
  secretKey: string;
  paymentFee: number;
}

const MEPS_PROVIDER_ID = 'MEPS';

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

function parseMepsConfig(additionalSettings: string | null): MepsConfig {
  // Demo default: test/sandbox mode on, blank credentials (supply your MEPS UAT details).
  const fallback: MepsConfig = {
    isSandbox: true,
    merchantId: '',
    terminalId: '',
    secretKey: '',
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
      isSandbox: sandbox === undefined ? true : sandbox === true,
      merchantId: readString(raw, 'merchantId', 'MerchantId'),
      terminalId: readString(raw, 'terminalId', 'TerminalId'),
      secretKey: readString(raw, 'secretKey', 'SecretKey'),
      paymentFee: typeof fee === 'number' ? fee : Number(fee) || 0,
    };
  } catch {
    return fallback;
  }
}

/**
 * Typed config form for MEPS (Middle East Payment Services), the Jordanian
 * gateway, with a sandbox/test-mode toggle for demo use. Fields serialize into
 * the provider's `additionalSettings` JSON the backend already stores, slotting
 * into the generic provider list at a more specific route than `payments/:id`.
 */
@Component({
  selector: 'app-admin-payment-meps-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './payment-meps-form.html',
})
export class AdminPaymentMepsForm {
  private readonly router = inject(Router);
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly providers = this.service.providersResource();
  protected readonly provider = computed(
    () => this.providers.value()?.find((p) => p.id === MEPS_PROVIDER_ID) ?? null,
  );

  protected readonly enabled = signal(false);
  protected readonly isSandbox = signal(true);
  protected readonly merchantId = signal('');
  protected readonly terminalId = signal('');
  protected readonly secretKey = signal('');
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
      const config = parseMepsConfig(p.additionalSettings);
      this.isSandbox.set(config.isSandbox);
      this.merchantId.set(config.merchantId);
      this.terminalId.set(config.terminalId);
      this.secretKey.set(config.secretKey);
      this.paymentFee.set(config.paymentFee);
    });
  }

  protected save(p: AdminPaymentProviderDto): void {
    this.saving.set(true);
    const config: MepsConfig = {
      isSandbox: this.isSandbox(),
      merchantId: this.merchantId().trim(),
      terminalId: this.terminalId().trim(),
      secretKey: this.secretKey().trim(),
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
