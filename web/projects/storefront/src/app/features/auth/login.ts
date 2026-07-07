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
  templateUrl: './login.html',
  styleUrl: './login.scss',
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
