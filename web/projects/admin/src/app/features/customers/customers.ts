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
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Customer directory: a searchable list of storefront shoppers (every non-admin
 * user) with their order count and lifetime spend. Creating and editing happen
 * on their own page (`/customers/new`, `/customers/:id`) — mirrors the user admin.
 */
@Component({
  selector: 'app-admin-customers',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, DatePipe, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'customers.title' | translate"
      [subtitle]="'customers.subtitle' | translate"
    >
      <a routerLink="/customers/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'customers.new' | translate }}
      </a>
    </app-page-header>

    <div class="card border-0 shadow-sm">
      <div class="card-body">
        <div class="search-box mb-3" style="max-width: 360px">
          <span class="search-box-icon"><lib-icon name="search" [size]="17" /></span>
          <input type="text" class="form-control" [placeholder]="'customers.search_ph' | translate"
            [attr.aria-label]="'customers.search_label' | translate"
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
            <table class="table table-hover align-middle mb-0" libTableCards>
              <thead>
                <tr>
                  <th scope="col">{{ 'customers.col_customer' | translate }}</th>
                  <th scope="col">{{ 'users.col_groups' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'nav.orders' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'customers.col_spend' | translate }}</th>
                  <th scope="col">{{ 'customers.col_joined' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (c of rows; track c.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/customers', c.id]" class="text-decoration-none fw-medium">{{ c.fullName }}</a>
                      <div class="small text-body-secondary">{{ c.email }}</div>
                    </td>
                    <td>
                      @for (g of c.customerGroups; track g) {
                        <span class="badge text-bg-light border me-1">{{ g }}</span>
                      } @empty {
                        <span class="text-body-secondary small">—</span>
                      }
                    </td>
                    <td class="text-end">{{ c.orderCount }}</td>
                    <td class="text-end">{{ c.totalSpent | money }}</td>
                    <td class="small">{{ c.createdOn | date: 'mediumDate' }}</td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/customers', c.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === c.id"
                          (click)="remove(c)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="text-center text-body-secondary py-4">
                      {{ 'customers.empty' | translate }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class AdminCustomers {
  private readonly service = inject(AdminCustomersService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly search = signal('');
  protected readonly list = this.service.listResource(() => ({
    query: this.search() || undefined,
  }));
  protected readonly deletingId = signal<number | null>(null);

  protected async remove(c: AdminCustomerListItem): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('customers.confirm_delete', { name: c.email ?? '#' + c.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
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
