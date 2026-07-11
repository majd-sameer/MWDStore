import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminWarehousesService,
  type AdminWarehouseDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Warehouse browser: a full-width list. Creating and editing happen on their own
 * page (`/warehouses/new`, `/warehouses/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-warehouses',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'warehouses.title' | translate"
      [subtitle]="'warehouses.subtitle' | translate"
    >
      <a routerLink="/warehouses/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'warehouses.new' | translate }}
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
                  <th scope="col">{{ 'common.name' | translate }}</th>
                  <th scope="col">{{ 'warehouses.col_location' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (w of rows; track w.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/warehouses', w.id]" class="text-decoration-none fw-medium">{{ w.name }}</a>
                    </td>
                    <td>
                      {{ w.city }}@if (w.city && w.stateOrProvinceName) {, }{{ w.stateOrProvinceName }}
                      <div class="small text-body-secondary">{{ w.countryName }}</div>
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/warehouses', w.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === w.id"
                          (click)="remove(w)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="3">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'warehouses.empty' | translate }}</div>
                        <a routerLink="/warehouses/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'warehouses.create_first' | translate }}
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
export class AdminWarehouses {
  private readonly service = inject(AdminWarehousesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly list = this.service.listResource();
  protected readonly deletingId = signal<number | null>(null);

  protected async remove(w: AdminWarehouseDto): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('warehouses.confirm_delete', { name: w.name ?? '#' + w.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) return;
    this.deletingId.set(w.id);
    this.service.delete(w.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('warehouses.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('warehouses.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
