import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { email, form, FormField as Control, required, submit } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService as AuthApi } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { Button, FormField } from 'ui';
import { firstError } from '../../shared/field-error';

interface ForgotPasswordModel {
  email: string;
}

/**
 * Requests a password-reset email. The API always answers 200 regardless of whether the account
 * exists (no account enumeration), so this page always shows the same neutral confirmation on
 * submit — it never reports "account not found".
 */
@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Control, FormField, Button, TranslatePipe],
  template: `
    <div class="auth-wrap">
      <div class="auth-card">
        <header class="auth-head">
          <img class="auth-logo" src="logo-color.png" alt="" />
          <h1 class="auth-title">{{ 'auth.forgot_title' | translate }}</h1>
          <p class="auth-subtitle">{{ 'auth.forgot_subtitle' | translate }}</p>
        </header>

        @if (sent()) {
          <div class="alert alert-success" role="status">{{ 'auth.forgot_sent' | translate }}</div>
          <p class="auth-alt">
            <a routerLink="/login">{{ 'auth.back_to_login' | translate }}</a>
          </p>
        } @else {
          @if (serverError(); as message) {
            <div class="alert alert-danger" role="alert">{{ message | translate }}</div>
          }

          <form (submit)="onSubmit($event)" novalidate>
            <lib-form-field
              [label]="'auth.email' | translate"
              controlId="email"
              [required]="true"
              [error]="emailError() | translate"
            >
              <input
                id="email"
                type="email"
                autocomplete="email"
                dir="ltr"
                class="form-control form-control-lg"
                [class.is-invalid]="!!emailError()"
                [formField]="f.email"
              />
            </lib-form-field>

            <button
              libButton
              variant="primary"
              size="lg"
              [block]="true"
              [disabled]="f().submitting()"
            >
              {{ (f().submitting() ? 'auth.sending' : 'auth.send_reset_link') | translate }}
            </button>
          </form>

          <p class="auth-alt">
            <a routerLink="/login">{{ 'auth.back_to_login' | translate }}</a>
          </p>
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
export class ForgotPassword {
  private readonly auth = inject(AuthApi);

  protected readonly model = signal<ForgotPasswordModel>({ email: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.email, { message: 'auth.email_required' });
    email(path.email, { message: 'auth.email_invalid' });
  });

  protected readonly serverError = signal<string | null>(null);
  protected readonly sent = signal(false);

  protected readonly emailError = computed(() => firstError(this.f.email()));

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      try {
        await firstValueFrom(this.auth.forgotPassword(this.model()));
        this.sent.set(true);
      } catch {
        // The API itself never reports failure for a valid request; this only fires on a genuine
        // network/server error, so it's fine to surface as a generic error rather than "check your email".
        this.serverError.set('auth.forgot_error');
      }
      return undefined;
    });
  }
}
