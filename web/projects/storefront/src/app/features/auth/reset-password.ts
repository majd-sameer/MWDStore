import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { form, FormField as Control, minLength, required, submit, validate } from '@angular/forms/signals';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService as AuthApi } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { Button, FormField } from 'ui';
import { firstError } from '../../shared/field-error';

interface ResetPasswordModel {
  password: string;
  confirmPassword: string;
}

/**
 * Consumes the `email` + `token` query params from the forgot-password email link, collects a new
 * password (with confirmation), and calls the API. Shows a clear error for an invalid/expired token
 * (the API returns 400 with model errors) and a success state linking back to login.
 */
@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Control, FormField, Button, TranslatePipe],
  template: `
    <div class="auth-wrap">
      <div class="auth-card">
        <header class="auth-head">
          <img class="auth-logo" src="logo-color.png" alt="" />
          <h1 class="auth-title">{{ 'auth.reset_title' | translate }}</h1>
          <p class="auth-subtitle">{{ 'auth.reset_subtitle' | translate }}</p>
        </header>

        @if (!email() || !token()) {
          <div class="alert alert-danger" role="alert">{{ 'auth.reset_link_invalid' | translate }}</div>
          <p class="auth-alt">
            <a routerLink="/forgot-password">{{ 'auth.request_new_link' | translate }}</a>
          </p>
        } @else if (done()) {
          <div class="alert alert-success" role="status">{{ 'auth.reset_success' | translate }}</div>
          <p class="auth-alt">
            <a routerLink="/login">{{ 'auth.sign_in' | translate }}</a>
          </p>
        } @else {
          @if (serverError(); as message) {
            <div class="alert alert-danger" role="alert">{{ message | translate }}</div>
          }

          <form (submit)="onSubmit($event)" novalidate>
            <lib-form-field
              [label]="'auth.new_password' | translate"
              controlId="password"
              [required]="true"
              [hint]="'auth.password_hint' | translate"
              [error]="passwordError() | translate"
            >
              <input
                id="password"
                type="password"
                autocomplete="new-password"
                dir="ltr"
                class="form-control form-control-lg"
                [class.is-invalid]="!!passwordError()"
                [formField]="f.password"
              />
            </lib-form-field>

            <lib-form-field
              [label]="'auth.confirm_password' | translate"
              controlId="confirmPassword"
              [required]="true"
              [error]="confirmPasswordError() | translate"
            >
              <input
                id="confirmPassword"
                type="password"
                autocomplete="new-password"
                dir="ltr"
                class="form-control form-control-lg"
                [class.is-invalid]="!!confirmPasswordError()"
                [formField]="f.confirmPassword"
              />
            </lib-form-field>

            <button
              libButton
              variant="primary"
              size="lg"
              [block]="true"
              [disabled]="f().submitting()"
            >
              {{ (f().submitting() ? 'auth.resetting' : 'auth.reset_password_action') | translate }}
            </button>
          </form>
        }
      </div>
    </div>
  `,
  styles: `
    .auth-wrap {
      max-inline-size: 460px;
      margin-inline: auto;
      padding-block: 1rem 2rem;
    }
    .auth-card {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      box-shadow: var(--shadow-md);
      padding: clamp(1.75rem, 4vw, 2.75rem);
    }
    .auth-head {
      text-align: center;
      margin-block-end: 1.75rem;
    }
    .auth-logo {
      block-size: 52px;
      inline-size: auto;
      margin-block-end: 1rem;
    }
    .auth-title {
      font-weight: 700;
      font-size: clamp(1.5rem, 3vw, 1.9rem);
      letter-spacing: -0.02em;
      margin-block-end: 0.4rem;
    }
    .auth-subtitle {
      color: var(--ink-2);
      margin-block-end: 0;
    }
    form button[libButton] {
      margin-block-start: 0.5rem;
    }
    .auth-alt {
      text-align: center;
      color: var(--ink-2);
      margin-block: 1.5rem 0;
    }
    .auth-alt a {
      font-weight: 600;
      color: var(--green-strong);
      text-decoration: none;
    }
    .auth-alt a:hover {
      text-decoration: underline;
    }
  `,
})
export class ResetPassword {
  private readonly auth = inject(AuthApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly email = signal<string | null>(
    this.route.snapshot.queryParamMap.get('email'),
  );
  protected readonly token = signal<string | null>(
    this.route.snapshot.queryParamMap.get('token'),
  );

  protected readonly model = signal<ResetPasswordModel>({ password: '', confirmPassword: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.password, { message: 'auth.password_required' });
    minLength(path.password, 8, { message: 'auth.password_min' });
    required(path.confirmPassword, { message: 'auth.confirm_password_required' });
    validate(path.confirmPassword, ({ valueOf }) =>
      valueOf(path.confirmPassword) === valueOf(path.password)
        ? null
        : { kind: 'mismatch', message: 'auth.password_mismatch' },
    );
  });

  protected readonly serverError = signal<string | null>(null);
  protected readonly done = signal(false);

  protected readonly passwordError = computed(() => firstError(this.f.password()));
  protected readonly confirmPasswordError = computed(() => firstError(this.f.confirmPassword()));

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const emailValue = this.email();
      const tokenValue = this.token();
      if (!emailValue || !tokenValue) {
        return undefined;
      }

      try {
        await firstValueFrom(
          this.auth.resetPassword({
            email: emailValue,
            token: tokenValue,
            newPassword: this.model().password,
          }),
        );
        this.done.set(true);
      } catch {
        this.serverError.set('auth.reset_link_invalid');
      }
      return undefined;
    });
  }
}
