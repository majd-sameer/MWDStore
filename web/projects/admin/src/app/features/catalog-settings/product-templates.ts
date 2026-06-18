import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminOperationsService,
  type AdminProductTemplateDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Product template browser (named attribute sets): a full-width list. Creating
 * and editing happen on their own page (`/product-templates/new`,
 * `/product-templates/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-product-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'templates.title' | translate"
      [subtitle]="'templates.subtitle' | translate"
    >
      <a routerLink="/product-templates/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'templates.new' | translate }}
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
                  <th scope="col">{{ 'templates.col_template' | translate }}</th>
                  <th scope="col">{{ 'nav.attributes' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (t of rows; track t.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/product-templates', t.id]" class="text-decoration-none fw-medium">{{ t.name }}</a>
                    </td>
                    <td>
                      @for (a of t.attributes; track a.id) {
                        <span class="badge text-bg-light border me-1">{{ a.name }}</span>
                      }
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/product-templates', t.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === t.id"
                          (click)="remove(t)"
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
                        <div class="empty-title">{{ 'templates.empty' | translate }}</div>
                        <a routerLink="/product-templates/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'templates.create_first' | translate }}
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
export class AdminProductTemplates {
  private readonly service = inject(AdminOperationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.templatesResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(t: AdminProductTemplateDto): void {
    if (!confirm(this.translate.instant('templates.confirm_delete', { name: t.name ?? '#' + t.id }))) {
      return;
    }
    this.deletingId.set(t.id);
    this.service.deleteTemplate(t.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('templates.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('templates.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
