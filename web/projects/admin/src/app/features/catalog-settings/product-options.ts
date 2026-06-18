import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminProductOptionsService,
  type AdminProductOptionListItem,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Product option browser (Color, Size, …): a full-width list. Creating and
 * editing happen on their own page (`/product-options/new`,
 * `/product-options/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-product-options',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'options.title' | translate"
      [subtitle]="'options.subtitle' | translate"
    >
      <a routerLink="/product-options/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'options.new' | translate }}
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
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th scope="col">{{ 'common.name' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (o of rows; track o.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/product-options', o.id]" class="text-decoration-none fw-medium">{{ o.name }}</a>
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/product-options', o.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === o.id"
                          (click)="remove(o)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="2">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'options.empty' | translate }}</div>
                        <div class="small">{{ 'options.empty_hint' | translate }}</div>
                        <a routerLink="/product-options/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'options.create_first' | translate }}
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
export class AdminProductOptions {
  private readonly service = inject(AdminProductOptionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(o: AdminProductOptionListItem): void {
    if (!confirm(this.translate.instant('options.confirm_delete', { name: o.name ?? '#' + o.id }))) {
      return;
    }
    this.deletingId.set(o.id);
    this.service.delete(o.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('options.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('options.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
