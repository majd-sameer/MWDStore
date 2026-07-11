import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminOperationsService, type AdminVendorDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Vendor browser: a full-width list. Creating and editing happen on their own
 * page (`/vendors/new`, `/vendors/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-vendors',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'vendors.title' | translate"
      [subtitle]="'vendors.subtitle' | translate"
    >
      <a routerLink="/vendors/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'vendors.new' | translate }}
      </a>
    </app-page-header>

    <div class="card border-0 shadow-sm">
      <div class="card-body">
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
                  <th scope="col">{{ 'vendors.col_vendor' | translate }}</th>
                  <th scope="col">{{ 'common.email' | translate }}</th>
                  <th scope="col">{{ 'common.status' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (v of rows; track v.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/vendors', v.id]" class="text-decoration-none fw-medium">{{ v.name }}</a>
                      <div class="small text-body-secondary">{{ v.slug }}</div>
                    </td>
                    <td class="text-body-secondary">{{ v.email }}</td>
                    <td>
                      @if (v.isActive) {
                        <span class="badge text-bg-success">{{ 'common.active' | translate }}</span>
                      } @else {
                        <span class="badge text-bg-secondary">{{ 'common.inactive' | translate }}</span>
                      }
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/vendors', v.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === v.id"
                          (click)="remove(v)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="4">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'vendors.empty' | translate }}</div>
                        <a routerLink="/vendors/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'vendors.create_first' | translate }}
                        </a>
                      </div>
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
export class AdminVendors {
  private readonly service = inject(AdminOperationsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.vendorsResource();
  protected readonly deletingId = signal<number | null>(null);

  protected async remove(v: AdminVendorDto): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('vendors.confirm_delete', { name: v.name ?? '#' + v.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.deletingId.set(v.id);
    this.service.deleteVendor(v.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('vendors.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('vendors.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
