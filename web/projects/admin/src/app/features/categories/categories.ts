import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminCategoriesService,
  type AdminCategoryDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Category browser: a full-width list with publish status and display order.
 * Creating and editing happen on their own page (`/categories/new`,
 * `/categories/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-categories',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'categories.title' | translate"
      [subtitle]="'categories.subtitle' | translate"
    >
      <a routerLink="/categories/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" />
        {{ 'categories.new' | translate }}
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
                  <th scope="col" class="text-end">{{ 'categories.col_order' | translate }}</th>
                  <th scope="col">{{ 'common.status' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (c of rows; track c.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/categories', c.id]" class="text-decoration-none fw-medium">
                        {{ c.name }}
                      </a>
                      <div class="small text-body-secondary">{{ c.slug }}</div>
                    </td>
                    <td class="text-end">{{ c.displayOrder }}</td>
                    <td>
                      @if (c.isPublished) {
                        <span class="badge text-bg-success">{{ 'categories.published' | translate }}</span>
                      } @else {
                        <span class="badge text-bg-secondary">{{ 'categories.hidden' | translate }}</span>
                      }
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a
                          [routerLink]="['/categories', c.id]"
                          class="action-btn"
                          [title]="'common.edit' | translate"
                        >
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
                    <td colspan="4">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'categories.empty' | translate }}</div>
                        <a routerLink="/categories/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'categories.create_first' | translate }}
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
export class AdminCategories {
  private readonly service = inject(AdminCategoriesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource(() => true);
  protected readonly deletingId = signal<number | null>(null);

  protected remove(c: AdminCategoryDto): void {
    if (!confirm(this.translate.instant('categories.confirm_delete', { name: c.name ?? '#' + c.id }))) {
      return;
    }
    this.deletingId.set(c.id);
    this.service.delete(c.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('categories.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('categories.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
