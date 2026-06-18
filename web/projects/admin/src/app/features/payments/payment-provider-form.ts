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
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/payments" class="text-decoration-none">← {{ 'payments.title' | translate }}</a>
    </nav>
    <app-page-header
      [title]="provider()
        ? ('payments.configure_title' | translate: { name: provider()!.name })
        : ('payments.configure_fallback' | translate)"
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
                <input type="checkbox" class="form-check-input" id="pay-enabled"
                  [checked]="enabled()" (change)="enabled.set($any($event.target).checked)" />
                <label class="form-check-label" for="pay-enabled">{{ 'common.enabled' | translate }}</label>
              </div>

              <label class="form-label" for="pay-settings">
                {{ 'payments.settings_label' | translate }}
              </label>
              <textarea id="pay-settings" rows="8"
                class="form-control font-monospace mb-2"
                [value]="settings()" (input)="settings.set($any($event.target).value)"></textarea>

              <div class="form-actions">
                <button type="button" libButton variant="primary" [disabled]="saving()" (click)="save(p)">
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
