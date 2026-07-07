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
  templateUrl: './users.html',
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
