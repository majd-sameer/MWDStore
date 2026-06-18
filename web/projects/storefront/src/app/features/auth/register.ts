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
  minLength,
  required,
  submit,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from 'core';
import { firstValueFrom } from 'rxjs';
import { Button, FormField, ToastService } from 'ui';
import { CartStore } from '../../core/cart.store';
import { firstError } from '../../shared/field-error';

interface RegisterModel {
  fullName: string;
  email: string;
  password: string;
}

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Control, FormField, Button, TranslatePipe],
  template: `
    <div class="auth-wrap">
      <div class="auth-card">
        <header class="auth-head">
          <img class="auth-logo" src="logo-color.png" alt="" />
          <h1 class="auth-title">{{ 'auth.register_title' | translate }}</h1>
          <p class="auth-subtitle">{{ 'auth.register_subtitle' | translate }}</p>
        </header>

        @if (serverError(); as message) {
          <div class="alert alert-danger" role="alert">{{ message | translate }}</div>
        }

        <form (submit)="onSubmit($event)" novalidate>
          <lib-form-field
            [label]="'auth.full_name' | translate"
            controlId="fullName"
            [error]="nameError() | translate"
          >
            <input
              id="fullName"
              type="text"
              autocomplete="name"
              dir="auto"
              class="form-control form-control-lg"
              [class.is-invalid]="!!nameError()"
              [formField]="f.fullName"
            />
          </lib-form-field>

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

          <button
            libButton
            variant="primary"
            size="lg"
            [block]="true"
            [disabled]="f().submitting()"
          >
            {{ (f().submitting() ? 'auth.creating' : 'auth.create_account') | translate }}
          </button>
        </form>

        <p class="auth-alt">
          {{ 'auth.has_account' | translate }}
          <a routerLink="/login">{{ 'auth.sign_in' | translate }}</a>
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
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cart = inject(CartStore);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly model = signal<RegisterModel>({
    fullName: '',
    email: '',
    password: '',
  });
  // Messages are i18n keys, resolved reactively by the `translate` pipe in the
  // template — so they re-translate live when the language is switched.
  protected readonly f = form(this.model, (path) => {
    required(path.email, { message: 'auth.email_required' });
    email(path.email, { message: 'auth.email_invalid' });
    required(path.password, { message: 'auth.password_required' });
    minLength(path.password, 8, { message: 'auth.password_min' });
  });

  protected readonly serverError = signal<string | null>(null);

  protected readonly nameError = computed(() => firstError(this.f.fullName()));
  protected readonly emailError = computed(() => firstError(this.f.email()));
  protected readonly passwordError = computed(() => firstError(this.f.password()));

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const { fullName, email: emailValue, password } = this.model();
      try {
        await firstValueFrom(
          this.auth.register({
            email: emailValue,
            password,
            fullName: fullName || null,
          }),
        );
        this.cart.reload();
        this.toast.success(this.translate.instant('auth.welcome_toast'));
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
        await this.router.navigateByUrl(returnUrl);
      } catch {
        this.serverError.set('auth.register_error');
      }
      return undefined;
    });
  }
}
