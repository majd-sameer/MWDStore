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
  templateUrl: './register.html',
  styleUrl: './register.scss',
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
