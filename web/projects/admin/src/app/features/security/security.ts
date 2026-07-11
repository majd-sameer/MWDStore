import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import QRCode from 'qrcode';
import { AuthService, type MfaSetupResponse } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ConfirmService, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Account-level two-factor authentication (TOTP) for the signed-in admin — mirrors the
 * flow storefront customers get, but scoped to `/api/account/mfa/*`. Three states:
 * disabled (offer setup), mid-setup (QR + shared key + code to confirm, then a one-time
 * recovery-codes reveal), and enabled (green badge + a code-gated disable form).
 */
@Component({
  selector: 'app-admin-security',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'security.title' | translate"
      [subtitle]="'security.subtitle' | translate"
    />

    <div class="card border-0 shadow-sm">
      <div class="card-header bg-body fw-semibold d-flex align-items-center gap-2">
        <lib-icon name="shield" [size]="18" />
        {{ 'security.mfa_title' | translate }}
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (recoveryCodes(); as codes) {
          <div class="alert alert-warning">{{ 'security.recovery_warning' | translate }}</div>
          <p class="fw-medium mb-2">{{ 'security.recovery_codes_title' | translate }}</p>
          <div class="font-monospace border rounded p-3 mb-3 bg-body-secondary">
            @for (code of codes; track code) {
              <div>{{ code }}</div>
            }
          </div>
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-outline-secondary btn-sm" (click)="copyRecoveryCodes(codes)">
              {{ 'security.copy_all' | translate }}
            </button>
            <button type="button" libButton variant="primary" size="sm" (click)="finishRecovery()">
              {{ 'security.recovery_done' | translate }}
            </button>
          </div>
        } @else if (enabled()) {
          <div class="d-flex align-items-center gap-2 mb-3">
            <span class="badge text-bg-success d-inline-flex align-items-center gap-1">
              <lib-icon name="check" [size]="13" />
              {{ 'security.enabled' | translate }}
            </span>
            <span class="small text-body-secondary">{{ 'security.enabled_explain' | translate }}</span>
          </div>

          <div style="max-width: 360px">
            <label class="form-label small" for="sec-disable-code">
              {{ 'security.code_label' | translate }}
            </label>
            <input
              id="sec-disable-code"
              type="text"
              inputmode="numeric"
              autocomplete="one-time-code"
              class="form-control mb-2"
              [class.is-invalid]="disableError()"
              [value]="disableCode()"
              (input)="disableCode.set($any($event.target).value)"
            />
            @if (disableError()) {
              <div class="alert alert-danger py-2 small">{{ 'security.code_invalid' | translate }}</div>
            }
            <button
              type="button"
              class="btn btn-outline-danger"
              [disabled]="disabling() || !disableCode()"
              (click)="disable()"
            >
              {{ (disabling() ? 'common.saving' : 'security.disable') | translate }}
            </button>
          </div>
        } @else if (settingUp()) {
          <p class="text-body-secondary small">{{ 'security.setup_explain' | translate }}</p>

          <div class="d-flex flex-wrap gap-4 mb-3">
            @if (qrDataUrl(); as qr) {
              <img [src]="qr" [alt]="'security.qr_alt' | translate" width="180" height="180" class="border rounded p-1" />
            }
            <div class="flex-grow-1" style="min-width: 220px">
              <div class="form-label small">{{ 'security.shared_key' | translate }}</div>
              <div class="d-flex gap-2 mb-3">
                <code class="form-control font-monospace small">{{ setupData()?.sharedKey }}</code>
                <button type="button" class="btn btn-outline-secondary btn-sm" (click)="copySharedKey()">
                  {{ 'common.copy' | translate }}
                </button>
              </div>

              <label class="form-label small" for="sec-enable-code">
                {{ 'security.code_label' | translate }}
              </label>
              <input
                id="sec-enable-code"
                type="text"
                inputmode="numeric"
                autocomplete="one-time-code"
                class="form-control mb-2"
                [class.is-invalid]="enableError()"
                [value]="enableCode()"
                (input)="enableCode.set($any($event.target).value)"
              />
              @if (enableError()) {
                <div class="alert alert-danger py-2 small">{{ 'security.code_invalid' | translate }}</div>
              }
              <div class="d-flex gap-2">
                <button
                  type="button"
                  libButton
                  variant="primary"
                  [disabled]="enabling() || !enableCode()"
                  (click)="enable()"
                >
                  {{ (enabling() ? 'common.saving' : 'security.enable') | translate }}
                </button>
                <button type="button" class="btn btn-outline-secondary" [disabled]="enabling()" (click)="cancelSetup()">
                  {{ 'common.cancel' | translate }}
                </button>
              </div>
            </div>
          </div>
        } @else {
          <p class="text-body-secondary">{{ 'security.disabled_explain' | translate }}</p>
          <button type="button" libButton variant="primary" [disabled]="settingUpLoading()" (click)="startSetup()">
            {{ (settingUpLoading() ? 'common.loading' : 'security.setup') | translate }}
          </button>
        }
      </div>
    </div>
  `,
})
export class AdminSecurity {
  private readonly auth = inject(AuthService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly loading = signal(true);
  protected readonly enabled = signal(false);

  protected readonly settingUp = signal(false);
  protected readonly settingUpLoading = signal(false);
  protected readonly setupData = signal<MfaSetupResponse | null>(null);
  protected readonly qrDataUrl = signal<string | null>(null);
  protected readonly enableCode = signal('');
  protected readonly enabling = signal(false);
  protected readonly enableError = signal(false);

  protected readonly recoveryCodes = signal<string[] | null>(null);

  protected readonly disableCode = signal('');
  protected readonly disabling = signal(false);
  protected readonly disableError = signal(false);

  constructor() {
    this.refreshStatus();
  }

  private refreshStatus(): void {
    this.loading.set(true);
    this.auth.mfaStatus().subscribe({
      next: (s) => {
        this.enabled.set(s.enabled);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  protected async startSetup(): Promise<void> {
    this.settingUpLoading.set(true);
    try {
      const setup = await firstValueFrom(this.auth.mfaSetup());
      this.setupData.set(setup);
      this.qrDataUrl.set(setup.authenticatorUri ? await QRCode.toDataURL(setup.authenticatorUri) : null);
      this.enableCode.set('');
      this.enableError.set(false);
      this.settingUp.set(true);
    } catch {
      this.toast.error(this.translate.instant('security.setup_failed'));
    } finally {
      this.settingUpLoading.set(false);
    }
  }

  protected cancelSetup(): void {
    this.settingUp.set(false);
    this.setupData.set(null);
    this.qrDataUrl.set(null);
    this.enableCode.set('');
    this.enableError.set(false);
  }

  protected copySharedKey(): void {
    const key = this.setupData()?.sharedKey;
    if (!key) {
      return;
    }
    void navigator.clipboard.writeText(key).then(() => this.toast.success(this.translate.instant('security.copy_ok')));
  }

  protected async enable(): Promise<void> {
    this.enableError.set(false);
    this.enabling.set(true);
    try {
      const result = await firstValueFrom(this.auth.mfaEnable({ code: this.enableCode() }));
      this.recoveryCodes.set(result.recoveryCodes ?? []);
      this.settingUp.set(false);
      this.setupData.set(null);
      this.qrDataUrl.set(null);
      this.enableCode.set('');
      this.enabled.set(true);
    } catch {
      this.enableError.set(true);
    } finally {
      this.enabling.set(false);
    }
  }

  protected copyRecoveryCodes(codes: string[]): void {
    void navigator.clipboard
      .writeText(codes.join('\n'))
      .then(() => this.toast.success(this.translate.instant('security.copy_ok')));
  }

  protected finishRecovery(): void {
    this.recoveryCodes.set(null);
    this.refreshStatus();
  }

  protected async disable(): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('security.confirm_disable'),
      okText: this.translate.instant('security.disable'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.disableError.set(false);
    this.disabling.set(true);
    try {
      await firstValueFrom(this.auth.mfaDisable({ code: this.disableCode() }));
      this.disableCode.set('');
      this.enabled.set(false);
      this.toast.success(this.translate.instant('security.disabled_ok'));
    } catch {
      this.disableError.set(true);
    } finally {
      this.disabling.set(false);
    }
  }
}
