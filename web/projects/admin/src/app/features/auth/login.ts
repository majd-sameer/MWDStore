import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  email,
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, LanguageService } from 'core';
import { firstValueFrom } from 'rxjs';
import { Button, FormField } from 'ui';
import { firstError } from '../../shared/field-error';

interface LoginModel {
  email: string;
  password: string;
}

/**
 * Admin sign-in. Authenticates via core's `AuthService` (JWT held in memory),
 * then verifies the account actually holds the `Admin` role before entering the
 * console — a non-admin who authenticates is signed back out with a clear
 * message rather than being bounced by the route guard. Includes a language
 * toggle since the authenticated topbar isn't available yet.
 */
@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, TranslatePipe],
  styles: `
    .lang-switch {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      border: 1px solid var(--line-strong);
      border-radius: 999px;
      background: var(--surface);
      color: var(--ink);
      padding: 0.3rem 0.85rem;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .lang-switch:hover {
      background: var(--surface-2);
    }
  `,
  template: `
    <div class="min-vh-100 d-flex align-items-center bg-body-tertiary">
      <div class="container">
        <div class="row justify-content-center">
          <div class="col-sm-9 col-md-7 col-lg-5 col-xl-4">
            <div class="text-end mb-2">
              <button
                type="button"
                class="lang-switch"
                [attr.aria-label]="'common.language' | translate"
                (click)="language.toggle()"
              >
                <i class="bi bi-translate" aria-hidden="true"></i>
                {{ 'common.language' | translate }}
              </button>
            </div>
            <div class="card shadow-sm border-0">
              <div class="card-body p-4 p-md-5">
                <div class="text-center mb-4">
                  <img
                    src="logo-color.png"
                    [alt]="'brand.name' | translate"
                    style="height: 84px; width: auto"
                    class="d-block mx-auto mb-2"
                  />
                  <span class="fs-5 fw-semibold">{{ 'brand.name' | translate }}</span>
                  <span class="badge text-bg-primary ms-2">Admin</span>
                </div>
                <h1 class="h5 text-center mb-4 text-body-secondary">
                  {{ 'login.title' | translate }}
                </h1>

                @if (serverError(); as messageKey) {
                  <div class="alert alert-danger" role="alert">
                    {{ messageKey | translate }}
                  </div>
                }

                @if (mfaChallengeToken(); as challenge) {
                  <form (submit)="onMfaSubmit($event)" novalidate>
                    <p class="text-body-secondary small">
                      {{ 'login.mfa_hint' | translate }}
                    </p>
                    <lib-form-field
                      [label]="'login.mfa_code' | translate"
                      controlId="mfa-code"
                      [required]="true"
                    >
                      <input
                        id="mfa-code"
                        type="text"
                        inputmode="numeric"
                        autocomplete="one-time-code"
                        class="form-control form-control-lg text-center"
                        dir="ltr"
                        [value]="mfaCode()"
                        (input)="mfaCode.set($any($event.target).value)"
                      />
                    </lib-form-field>

                    <button
                      libButton
                      variant="primary"
                      size="lg"
                      class="py-2"
                      [block]="true"
                      [disabled]="verifying() || !mfaCode().trim()"
                    >
                      {{ (verifying() ? 'login.signing_in' : 'login.mfa_verify') | translate }}
                    </button>
                    <button
                      libButton
                      type="button"
                      variant="link"
                      class="mt-2"
                      [block]="true"
                      (click)="backToCredentials()"
                    >
                      {{ 'login.mfa_back' | translate }}
                    </button>
                  </form>
                } @else {
                  <form (submit)="onSubmit($event)" novalidate>
                    <lib-form-field
                      [label]="'login.email' | translate"
                      controlId="email"
                      [required]="true"
                      [error]="emailError()"
                    >
                      <input
                        id="email"
                        type="email"
                        autocomplete="username"
                        class="form-control"
                        [class.is-invalid]="!!emailError()"
                        [formField]="f.email"
                      />
                    </lib-form-field>

                    <lib-form-field
                      [label]="'login.password' | translate"
                      controlId="password"
                      [required]="true"
                      [error]="passwordError()"
                    >
                      <input
                        id="password"
                        type="password"
                        autocomplete="current-password"
                        class="form-control"
                        [class.is-invalid]="!!passwordError()"
                        [formField]="f.password"
                      />
                    </lib-form-field>

                    <button
                      libButton
                      variant="primary"
                      size="lg"
                      class="py-2 mt-1"
                      [block]="true"
                      [disabled]="f().submitting()"
                    >
                      {{ (f().submitting() ? 'login.signing_in' : 'login.signin') | translate }}
                    </button>
                  </form>
                }
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  protected readonly language = inject(LanguageService);

  protected readonly model = signal<LoginModel>({ email: '', password: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.email, { message: () => this.translate.instant('login.email_required') });
    email(path.email, { message: () => this.translate.instant('login.email_invalid') });
    required(path.password, { message: () => this.translate.instant('login.password_required') });
  });

  /** Holds a translation key (resolved in the template) so it re-renders on language change. */
  protected readonly serverError = signal<string | null>(null);

  protected readonly emailError = computed(() => firstError(this.f.email()));
  protected readonly passwordError = computed(() =>
    firstError(this.f.password()),
  );

  /** Non-null while a signed-in-but-MFA-pending challenge is active. */
  protected readonly mfaChallengeToken = signal<string | null>(null);
  protected readonly mfaCode = signal('');
  protected readonly verifying = signal(false);

  protected onSubmit(event: Event): void {
    event.preventDefault();
    // Clear any previous error and strip pasted whitespace *before* validation —
    // submit() skips the action entirely when the form is invalid, so anything
    // inside it never runs for an invalid form (stale errors would linger).
    this.serverError.set(null);
    this.model.update((m) => ({ ...m, email: m.email.trim() }));
    void submit(this.f, async () => {
      let response;
      try {
        response = await firstValueFrom(this.auth.login(this.model()));
      } catch (err) {
        this.serverError.set(
          err instanceof HttpErrorResponse && err.status === 0
            ? 'login.no_server'
            : 'login.invalid',
        );
        return undefined;
      }

      if (response.mfaRequired && response.challengeToken) {
        this.mfaChallengeToken.set(response.challengeToken);
        this.mfaCode.set('');
        return undefined;
      }

      await this.enterConsole();
      return undefined;
    });
  }

  protected onMfaSubmit(event: Event): void {
    event.preventDefault();
    const challenge = this.mfaChallengeToken();
    const code = this.mfaCode().trim();
    if (!challenge || !code || this.verifying()) {
      return;
    }
    this.serverError.set(null);
    this.verifying.set(true);
    void firstValueFrom(this.auth.mfaVerify(challenge, code))
      .then(() => this.enterConsole())
      .catch(() => this.serverError.set('login.mfa_invalid'))
      .finally(() => this.verifying.set(false));
  }

  protected backToCredentials(): void {
    this.mfaChallengeToken.set(null);
    this.mfaCode.set('');
    this.serverError.set(null);
  }

  private async enterConsole(): Promise<void> {
    if (!this.auth.hasRole('admin')) {
      this.auth.clearSession();
      this.mfaChallengeToken.set(null);
      this.serverError.set('login.no_access');
      return;
    }

    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
    await this.router.navigateByUrl(returnUrl);
  }
}
