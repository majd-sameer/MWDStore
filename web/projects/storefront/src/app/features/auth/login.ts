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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';
import { firstValueFrom } from 'rxjs';
import { Button, FormField } from 'ui';
import { CartStore } from '../../core/cart.store';
import { firstError } from '../../shared/field-error';

interface LoginModel {
  email: string;
  password: string;
}

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Control, FormField, Button, TranslatePipe],
  template: `
    <div class="auth-wrap">
      <div class="auth-card">
        <header class="auth-head">
          <img class="auth-logo" src="logo-color.png" alt="" />
          <h1 class="auth-title">{{ 'auth.login_title' | translate }}</h1>
          <p class="auth-subtitle">{{ 'auth.login_subtitle' | translate }}</p>
        </header>

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

          <lib-form-field
            [label]="'auth.password' | translate"
            controlId="password"
            [required]="true"
            [error]="passwordError() | translate"
          >
            <input
              id="password"
              type="password"
              autocomplete="current-password"
              dir="ltr"
              class="form-control form-control-lg"
              [class.is-invalid]="!!passwordError()"
              [formField]="f.password"
            />
          </lib-form-field>

          <p class="auth-forgot">
            <a routerLink="/forgot-password">{{ 'auth.forgot_password_link' | translate }}</a>
          </p>

          <button
            libButton
            variant="primary"
            size="lg"
            [block]="true"
            [disabled]="f().submitting()"
          >
            {{ (f().submitting() ? 'auth.signing_in' : 'auth.sign_in') | translate }}
          </button>
        </form>

        <p class="auth-alt">
          {{ 'auth.no_account' | translate }}
          <a routerLink="/register">{{ 'auth.create_account' | translate }}</a>
        </p>
      </div>

      <p class="auth-note">{{ 'auth.supports_note' | translate }}</p>
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
    .auth-forgot {
      text-align: end;
      margin-block: -0.5rem 0.75rem;
    }
    .auth-forgot a {
      font-size: 0.9rem;
      font-weight: 600;
      color: var(--green-strong);
      text-decoration: none;
    }
    .auth-forgot a:hover {
      text-decoration: underline;
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
    .auth-note {
      text-align: center;
      color: var(--ink-3);
      font-size: 0.85rem;
      margin-block: 1.25rem 0;
    }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cart = inject(CartStore);

  protected readonly model = signal<LoginModel>({ email: '', password: '' });
  // Messages are i18n keys, resolved reactively by the `translate` pipe in the
  // template — so they re-translate live when the language is switched.
  protected readonly f = form(this.model, (path) => {
    required(path.email, { message: 'auth.email_required' });
    email(path.email, { message: 'auth.email_invalid' });
    required(path.password, { message: 'auth.password_required' });
  });

  protected readonly serverError = signal<string | null>(null);

  protected readonly emailError = computed(() => firstError(this.f.email()));
  protected readonly passwordError = computed(() => firstError(this.f.password()));

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      try {
        await firstValueFrom(this.auth.login(this.model()));
        this.cart.reload();
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
        await this.router.navigateByUrl(returnUrl);
      } catch {
        this.serverError.set('auth.invalid_credentials');
      }
      return undefined;
    });
  }
}
