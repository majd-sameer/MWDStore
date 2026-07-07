import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MoneyPipe } from 'core';
import { RouterLink } from '@angular/router';
import {
  AdminCustomersService,
  type AdminCustomerListItem,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Customer directory: a searchable list of storefront shoppers (every non-admin
 * user) with their order count and lifetime spend. Creating and editing happen
 * on their own page (`/customers/new`, `/customers/:id`) — mirrors the user admin.
 */
@Component({
  selector: 'app-admin-customers',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, DatePipe, Icon, TranslatePipe, PageHeader],
  templateUrl: './customers.html',
})
export class AdminCustomers {
  private readonly service = inject(AdminCustomersService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly search = signal('');
  protected readonly list = this.service.listResource(() => ({
    query: this.search() || undefined,
  }));
  protected readonly deletingId = signal<number | null>(null);

  protected remove(c: AdminCustomerListItem): void {
    if (!confirm(this.translate.instant('customers.confirm_delete', { name: c.email ?? '#' + c.id }))) {
      return;
    }
    this.deletingId.set(c.id);
    this.service.delete(c.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('customers.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('customers.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
