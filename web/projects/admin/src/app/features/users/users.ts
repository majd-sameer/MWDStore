import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
import { AdminUsersService, type AdminUserListItem } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { STAFF_ROLES } from '../../core/roles';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

/**
 * User browser: a searchable list of staff users, filterable by role. Creating
 * and editing a user happen on their own page (`/users/new`, `/users/:id`).
 */
@Component({
  selector: 'app-admin-users',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    FormsModule,
    NgSelectModule,
    Icon,
    TranslatePipe,
    PageHeader,
    TableSkeleton,
    TableFooter,
  ],
  templateUrl: './users.html',
})
export class AdminUsers {
  private readonly service = inject(AdminUsersService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  /** Role options for the filter dropdown, labelled via `roles.<name>`. */
  protected readonly roleOptions = STAFF_ROLES.map((name) => ({
    value: name,
    label: this.translate.instant('roles.' + name),
  }));

  protected readonly search = signal('');
  protected readonly role = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly list = this.service.listResource(() => ({
    query: this.search() || undefined,
    role: this.role() ?? undefined,
    page: this.page(),
    pageSize: this.pageSize(),
  }));
  protected readonly rows = computed(() => this.list.value()?.items ?? []);
  protected readonly total = computed(() => this.list.value()?.total ?? 0);
  protected readonly deletingId = signal<number | null>(null);

  protected setSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  protected setRole(value: string | null): void {
    this.role.set(value);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

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
}
