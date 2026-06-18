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
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/users" class="text-decoration-none">← {{ 'users.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'users.new_title' : 'users.edit_title') | translate" />

    @if (!isNew() && loading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && loadError()) {
      <div class="alert alert-danger">{{ 'users.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-8">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field [label]="'common.full_name' | translate" controlId="usr-name" [required]="true"
                  [error]="err(f.fullName())">
                  <input id="usr-name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.fullName())" [formField]="f.fullName" />
                </lib-form-field>
                @if (isNew()) {
                  <lib-form-field [label]="'common.email' | translate" controlId="usr-email" [required]="true"
                    [error]="err(f.email())">
                    <input id="usr-email" type="email" class="form-control"
                      [class.is-invalid]="!!err(f.email())" [formField]="f.email" />
                  </lib-form-field>
                  <lib-form-field [label]="'login.password' | translate" controlId="usr-pass" [required]="true"
                    [error]="err(f.password())">
                    <input id="usr-pass" type="password" class="form-control"
                      [class.is-invalid]="!!err(f.password())" [formField]="f.password" />
                  </lib-form-field>
                } @else {
                  <lib-form-field [label]="'common.email' | translate" controlId="usr-email-ro"
                    [hint]="'users.email_ro_hint' | translate">
                    <input id="usr-email-ro" type="email" class="form-control" readonly
                      [value]="model().email" />
                  </lib-form-field>
                }
                <lib-form-field [label]="'common.phone' | translate" controlId="usr-phone">
                  <input id="usr-phone" type="text" class="form-control" [formField]="f.phoneNumber" />
                </lib-form-field>

                <div class="form-label">{{ 'users.col_roles' | translate }}</div>
                <div class="border rounded p-2 mb-3">
                  @for (r of roles.value() ?? []; track r.id) {
                    <div class="form-check">
                      <input type="checkbox" class="form-check-input" id="usr-role-{{ r.id }}"
                        [checked]="selectedRoles().includes(r.name ?? '')"
                        (change)="toggleRole(r.name ?? '')" />
                      <label class="form-check-label" for="usr-role-{{ r.id }}">{{ r.name }}</label>
                    </div>
                  }
                </div>

                <div class="form-label">{{ 'users.groups_title' | translate }}</div>
                <div class="border rounded p-2 mb-3">
                  @for (g of groups.value() ?? []; track g.id) {
                    <div class="form-check">
                      <input type="checkbox" class="form-check-input" id="usr-grp-{{ g.id }}"
                        [checked]="selectedGroupIds().includes(g.id)"
                        (change)="toggleGroup(g.id)" />
                      <label class="form-check-label" for="usr-grp-{{ g.id }}">{{ g.name }}</label>
                    </div>
                  } @empty {
                    <span class="text-body-secondary small">{{ 'users.no_groups_defined' | translate }}</span>
                  }
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'users.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/users" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
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
