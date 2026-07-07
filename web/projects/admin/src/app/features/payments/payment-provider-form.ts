import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AdminPaymentsService,
  type AdminPaymentProviderDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Configure a payment provider on its own page. Providers are a fixed gateway
 * set (no create/delete), so this is edit-only: toggle enabled and edit the
 * gateway's JSON settings. Seeds from the providers list resource (string id).
 */
@Component({
  selector: 'app-admin-payment-provider-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './payment-provider-form.html',
})
export class AdminPaymentProviderForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminPaymentsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly providerId = computed(() => this.idParam().get('id') ?? '');

  protected readonly providers = this.service.providersResource();
  protected readonly provider = computed(
    () => this.providers.value()?.find((p) => p.id === this.providerId()) ?? null,
  );

  protected readonly enabled = signal(false);
  protected readonly settings = signal('');
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
      this.settings.set(p.additionalSettings ?? '');
    });
  }

  protected save(p: AdminPaymentProviderDto): void {
    this.saving.set(true);
    this.service
      .updateProvider(p.id, {
        name: p.name ?? p.id,
        isEnabled: this.enabled(),
        additionalSettings: this.settings().trim() || null,
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
