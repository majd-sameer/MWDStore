import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCmsService, type AdminPageDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/** `AdminPageDto` doesn't yet declare `hasEnglish` in the shared `data-access` models — see
 * `page-form.ts` for the matching extended request/response shape. */
interface AdminPageDtoEn extends AdminPageDto {
  hasEnglish: boolean;
}

/**
 * CMS page browser: a full-width list. Creating and editing happen on their own
 * page (`/pages/new`, `/pages/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-pages',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'pages.title' | translate"
      [subtitle]="'pages.subtitle' | translate"
    >
      <a routerLink="/pages/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'pages.new' | translate }}
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
                  <th scope="col">{{ 'pages.col_page' | translate }}</th>
                  <th scope="col">{{ 'common.status' | translate }}</th>
                  <th scope="col">{{ 'pages.col_language' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (p of asEn(rows); track p.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/pages', p.id]" class="text-decoration-none fw-medium">{{ p.name }}</a>
                      <div class="small text-body-secondary">/{{ p.slug }}</div>
                    </td>
                    <td>
                      @if (p.isPublished) {
                        <span class="badge text-bg-success">{{ 'common.published' | translate }}</span>
                      } @else {
                        <span class="badge text-bg-secondary">{{ 'common.draft' | translate }}</span>
                      }
                    </td>
                    <td>
                      @if (p.hasEnglish) {
                        <span class="badge text-bg-info-subtle text-info-emphasis">{{ 'pages.lang_ar_en' | translate }}</span>
                      } @else {
                        <span class="badge text-bg-light text-body-secondary">{{ 'pages.lang_ar_only' | translate }}</span>
                      }
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/pages', p.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === p.id"
                          (click)="remove(p)"
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
                        <div class="empty-title">{{ 'pages.empty' | translate }}</div>
                        <a routerLink="/pages/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'pages.create_first' | translate }}
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
export class AdminPages {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly list = this.service.pagesResource();
  protected readonly deletingId = signal<number | null>(null);

  /** Narrows the list rows to the extended DTO shape that carries `hasEnglish`. */
  protected asEn(rows: AdminPageDto[]): AdminPageDtoEn[] {
    return rows as AdminPageDtoEn[];
  }

  protected async remove(p: AdminPageDto): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('pages.confirm_delete', { name: p.name ?? '#' + p.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.deletingId.set(p.id);
    this.service.deletePage(p.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('pages.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('pages.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
