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
import { AdminUsersService } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface UserModel {
  email: string;
  password: string;
  fullName: string;
  phoneNumber: string;
}

function emptyModel(): UserModel {
  return { email: '', password: '', fullName: '', phoneNumber: '' };
}

/**
 * Create / edit a user on its own page (mirrors the product form). Edit mode
 * fetches the full detail (`GET /api/admin/users/{id}`) to seed profile, roles
 * and group membership. Customer groups themselves are managed on the list page.
 */
@Component({
  selector: 'app-admin-user-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './user-form.html',
})
export class AdminUserForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminUsersService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly userId = computed(() => Number(this.idParam().get('id')));

  protected readonly roles = this.service.rolesResource();
  protected readonly groups = this.service.groupsResource();

  protected readonly loading = signal(false);
  protected readonly loadError = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly selectedRoles = signal<string[]>([]);
  protected readonly selectedGroupIds = signal<number[]>([]);

  protected readonly model = signal<UserModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.fullName, { message: 'Full name is required' });
    required(path.email, { message: 'Email is required' });
  });
  protected readonly err = firstError;

  constructor() {
    if (!this.isNew()) {
      this.loading.set(true);
      this.service.get(this.userId()).subscribe({
        next: (detail) => {
          this.model.set({
            email: detail.email ?? '',
            password: '',
            fullName: detail.fullName ?? '',
            phoneNumber: detail.phoneNumber ?? '',
          });
          this.selectedRoles.set(detail.roles);
          this.selectedGroupIds.set(detail.customerGroupIds);
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
    }
  }

  protected toggleRole(name: string): void {
    this.selectedRoles.update((roles) =>
      roles.includes(name) ? roles.filter((r) => r !== name) : [...roles, name],
    );
  }

  protected toggleGroup(id: number): void {
    this.selectedGroupIds.update((ids) =>
      ids.includes(id) ? ids.filter((g) => g !== id) : [...ids, id],
    );
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      try {
        if (this.isNew()) {
          if (!m.password) {
            this.serverError.set(this.translate.instant('users.password_required_new'));
            return undefined;
          }
          await firstValueFrom(
            this.service.create({
              email: m.email,
              password: m.password,
              fullName: m.fullName,
              phoneNumber: m.phoneNumber || null,
              roles: this.selectedRoles(),
              customerGroupIds: this.selectedGroupIds(),
            }),
          );
          this.toast.success(this.translate.instant('users.created_ok'));
        } else {
          await firstValueFrom(
            this.service.update(this.userId(), {
              fullName: m.fullName,
              phoneNumber: m.phoneNumber || null,
              roles: this.selectedRoles(),
              customerGroupIds: this.selectedGroupIds(),
            }),
          );
          this.toast.success(this.translate.instant('users.updated_ok'));
        }
        await this.router.navigate(['/users']);
      } catch {
        this.serverError.set(this.translate.instant('users.save_failed'));
      }
      return undefined;
    });
  }
}
