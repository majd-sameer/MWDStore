import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminBrandsService, type AdminBrandDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/** The backend also returns `hasEnglish` — kept as a local extension (see brand-form.ts). */
interface AdminBrandDtoEn extends AdminBrandDto {
  hasEnglish?: boolean;
}

/**
 * Brand browser: a full-width list. Creating and editing happen on their own
 * page (`/brands/new`, `/brands/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-brands',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'brands.title' | translate"
      [subtitle]="'brands.subtitle' | translate"
    >
      <a routerLink="/brands/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'brands.new' | translate }}
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
        } @else if (list.value()) {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0" libTableCards>
              <thead>
                <tr>
                  <th scope="col">{{ 'common.name' | translate }}</th>
                  <th scope="col">{{ 'common.status' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (b of rows(); track b.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/brands', b.id]" class="text-decoration-none fw-medium">{{ b.name }}</a>
                      @if (!b.hasEnglish) {
                        <span
                          class="badge text-bg-warning-subtle text-warning-emphasis ms-1"
                          [title]="'common.en_missing' | translate"
                        >
                          {{ 'common.en_missing' | translate }}
                        </span>
                      }
                      <div class="small text-body-secondary">{{ b.slug }}</div>
                    </td>
                    <td>
                      @if (b.isPublished) {
                        <span class="badge text-bg-success">{{ 'common.published' | translate }}</span>
                      } @else {
                        <span class="badge text-bg-secondary">{{ 'common.hidden' | translate }}</span>
                      }
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/brands', b.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === b.id"
                          (click)="remove(b)"
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
                        <div class="empty-title">{{ 'brands.empty' | translate }}</div>
                        <a routerLink="/brands/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'brands.create_first' | translate }}
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
export class AdminBrands {
  private readonly service = inject(AdminBrandsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly list = this.service.listResource(() => true);
  protected readonly rows = computed(() => (this.list.value() ?? []) as AdminBrandDtoEn[]);
  protected readonly deletingId = signal<number | null>(null);

  protected async remove(b: AdminBrandDtoEn): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('brands.confirm_delete', { name: b.name ?? '#' + b.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) return;
    this.deletingId.set(b.id);
    this.service.delete(b.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('brands.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('brands.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
