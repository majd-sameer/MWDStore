import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminCustomersService } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface CustomerModel {
  email: string;
  password: string;
  fullName: string;
  phoneNumber: string;
}

function emptyModel(): CustomerModel {
  return { email: '', password: '', fullName: '', phoneNumber: '' };
}

/**
 * Create / edit a customer on its own page (mirrors the user form, without roles).
 * Edit mode fetches the full detail (`GET /api/admin/customers/{id}`) to seed
 * the profile.
 */
@Component({
  selector: 'app-admin-customer-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './customer-form.html',
})
export class AdminCustomerForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCustomersService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly customerId = computed(() => Number(this.idParam().get('id')));

  protected readonly loading = signal(false);
  protected readonly loadError = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected readonly model = signal<CustomerModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.fullName, { message: 'Full name is required' });
    required(path.email, { message: 'Email is required' });
  });
  protected readonly err = firstError;

  constructor() {
    if (!this.isNew()) {
      this.loading.set(true);
      this.service.get(this.customerId()).subscribe({
        next: (detail) => {
          this.model.set({
            email: detail.email ?? '',
            password: '',
            fullName: detail.fullName ?? '',
            phoneNumber: detail.phoneNumber ?? '',
          });
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
    }
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      try {
        if (this.isNew()) {
          if (!m.password) {
            this.serverError.set(this.translate.instant('customers.password_required_new'));
            return undefined;
          }
          await firstValueFrom(
            this.service.create({
              email: m.email,
              password: m.password,
              fullName: m.fullName,
              phoneNumber: m.phoneNumber || null,
            }),
          );
          this.toast.success(this.translate.instant('customers.created_ok'));
        } else {
          await firstValueFrom(
            this.service.update(this.customerId(), {
              fullName: m.fullName,
              phoneNumber: m.phoneNumber || null,
            }),
          );
          this.toast.success(this.translate.instant('customers.updated_ok'));
        }
        await this.router.navigate(['/customers']);
      } catch {
        this.serverError.set(this.translate.instant('customers.save_failed'));
      }
      return undefined;
    });
  }
}
