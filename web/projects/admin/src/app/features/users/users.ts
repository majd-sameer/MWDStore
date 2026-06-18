import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  AdminUsersService,
  type AdminUserListItem,
  type CustomerGroupUpsertRequest,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * User browser: a searchable list with the customer-group manager alongside.
 * Creating and editing a user happen on their own page (`/users/new`,
 * `/users/:id`); customer groups stay here as a secondary entity.
 */
@Component({
  selector: 'app-admin-users',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Button, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'users.title' | translate"
      [subtitle]="'users.subtitle' | translate"
    >
      <a routerLink="/users/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'users.new' | translate }}
      </a>
    </app-page-header>

    <div class="row g-4">
      <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
          <div class="card-body">
            <div class="search-box mb-3" style="max-width: 360px">
              <span class="search-box-icon"><lib-icon name="search" [size]="17" /></span>
              <input type="text" class="form-control" [placeholder]="'users.search_ph' | translate"
                [attr.aria-label]="'users.search_label' | translate"
                (input)="search.set($any($event.target).value)" />
            </div>

            @if (list.isLoading()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
                </div>
              </div>
            } @else if (list.error()) {
              <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
            } @else if (list.value(); as rows) {
              <div class="table-responsive">
                <table class="table table-hover align-middle mb-0">
                  <thead>
                    <tr>
                      <th scope="col">{{ 'users.col_user' | translate }}</th>
                      <th scope="col">{{ 'users.col_roles' | translate }}</th>
                      <th scope="col">{{ 'users.col_groups' | translate }}</th>
                      <th scope="col">{{ 'common.created' | translate }}</th>
                      <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (u of rows; track u.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/users', u.id]" class="text-decoration-none fw-medium">{{ u.fullName }}</a>
                          <div class="small text-body-secondary">{{ u.email }}</div>
                        </td>
                        <td>
                          @for (r of u.roles; track r) {
                            <span class="badge text-bg-secondary me-1">{{ r }}</span>
                          }
                        </td>
                        <td>
                          @for (g of u.customerGroups; track g) {
                            <span class="badge text-bg-light border me-1">{{ g }}</span>
                          }
                        </td>
                        <td class="small">{{ u.createdOn | date: 'mediumDate' }}</td>
                        <td class="text-end">
                          <span class="d-inline-flex gap-1">
                            <a [routerLink]="['/users', u.id]" class="action-btn" [title]="'common.edit' | translate">
                              <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                            </a>
                            <button
                              type="button"
                              class="action-btn action-btn-danger"
                              [title]="'common.delete' | translate"
                              [disabled]="deletingId() === u.id"
                              (click)="remove(u)"
                            >
                              <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                            </button>
                          </span>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="5" class="text-center text-body-secondary py-4">
                          {{ 'users.empty' | translate }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        </div>
      </div>

      <div class="col-lg-4">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'users.groups_title' | translate }}</div>
          <div class="card-body">
            @for (g of groups.value() ?? []; track g.id) {
              <div class="d-flex align-items-center gap-2 mb-2">
                <input type="text" class="form-control form-control-sm" [value]="g.name"
                  (change)="renameGroup(g.id, $any($event.target).value, g.isActive)" />
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="grp-active-{{ g.id }}"
                    [checked]="g.isActive"
                    (change)="renameGroup(g.id, g.name ?? '', $any($event.target).checked)" />
                  <label class="form-check-label small" for="grp-active-{{ g.id }}">
                    {{ 'common.active' | translate }}
                  </label>
                </div>
                <button type="button" class="btn btn-sm btn-outline-danger"
                  (click)="removeGroup(g.id, g.name)">✕</button>
              </div>
            } @empty {
              <p class="text-body-secondary small">{{ 'users.no_groups' | translate }}</p>
            }
            <div class="d-flex gap-2 mt-3">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'users.new_group_ph' | translate" #groupName />
              <button type="button" libButton variant="secondary" [outline]="true"
                (click)="addGroup(groupName)">
                {{ 'common.add' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminUsers {
  private readonly service = inject(AdminUsersService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly search = signal('');
  protected readonly list = this.service.listResource(() => ({
    query: this.search() || undefined,
  }));
  protected readonly groups = this.service.groupsResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(u: AdminUserListItem): void {
    if (!confirm(this.translate.instant('users.confirm_delete', { name: u.email ?? '#' + u.id }))) {
      return;
    }
    this.deletingId.set(u.id);
    this.service.delete(u.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('users.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('users.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }

  // ----- Customer groups -------------------------------------------------------

  protected addGroup(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    const body: CustomerGroupUpsertRequest = { name, isActive: true };
    this.service.createGroup(body).subscribe({
      next: () => {
        input.value = '';
        this.groups.reload();
      },
      error: () => this.toast.error(this.translate.instant('users.group_create_failed')),
    });
  }

  protected renameGroup(id: number, name: string, isActive: boolean): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    this.service.updateGroup(id, { name: trimmed, isActive }).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('users.group_updated'));
        this.groups.reload();
      },
      error: () => this.toast.error(this.translate.instant('users.group_update_failed')),
    });
  }

  protected removeGroup(id: number, name: string | null): void {
    if (!confirm(this.translate.instant('users.confirm_delete_group', { name: name ?? '' }))) {
      return;
    }
    this.service.deleteGroup(id).subscribe({
      next: () => this.groups.reload(),
      error: () => this.toast.error(this.translate.instant('users.group_delete_failed')),
    });
  }
}
