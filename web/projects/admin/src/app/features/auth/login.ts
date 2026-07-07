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
import { STAFF_ROLES } from '../../core/roles';

interface LoginModel {
  email: string;
  password: string;
}

/**
 * Admin sign-in. Authenticates via core's `AuthService` (JWT held in memory),
 * then verifies the account holds at least one staff role before entering the
 * console — a customer who authenticates is signed back out with a clear
 * message rather than being bounced by the route guard. Includes a language
 * toggle since the authenticated topbar isn't available yet.
 */
@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, TranslatePipe],
  styleUrl: './login.scss',
  templateUrl: './login.html',
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

  protected onSubmit(event: Event): void {
    event.preventDefault();
    // Clear any previous error and strip pasted whitespace *before* validation —
    // submit() skips the action entirely when the form is invalid, so anything
    // inside it never runs for an invalid form (stale errors would linger).
    this.serverError.set(null);
    this.model.update((m) => ({ ...m, email: m.email.trim() }));
    void submit(this.f, async () => {
      try {
        await firstValueFrom(this.auth.login(this.model()));
      } catch (err) {
        this.serverError.set(
          err instanceof HttpErrorResponse && err.status === 0
            ? 'login.no_server'
            : 'login.invalid',
        );
        return undefined;
      }

      if (!this.auth.hasAnyRole(STAFF_ROLES)) {
        this.auth.clearSession();
        this.serverError.set('login.no_access');
        return undefined;
      }

      const returnUrl =
        this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
      await this.router.navigateByUrl(returnUrl);
      return undefined;
    });
  }
}
