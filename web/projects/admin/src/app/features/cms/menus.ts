import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCmsService, type AdminMenuDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Menu browser: a full-width list of navigation menus. Creating and editing a
 * menu (and its items) happen on their own page (`/menus/new`, `/menus/:id`),
 * mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-menus',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'menus.title' | translate"
      [subtitle]="'menus.subtitle' | translate"
    >
      <a routerLink="/menus/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'menus.new' | translate }}
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
                  <th scope="col">{{ 'menus.col_menu' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'menus.col_items' | translate }}</th>
                  <th scope="col">{{ 'common.published' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (m of rows; track m.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/menus', m.id]" class="text-decoration-none fw-medium">{{ m.name }}</a>
                      @if (m.isSystem) {
                        <span class="badge text-bg-info ms-1">{{ 'menus.system_badge' | translate }}</span>
                      }
                    </td>
                    <td class="text-end">{{ m.items.length }}</td>
                    <td>
                      <div class="form-check form-switch">
                        <input type="checkbox" class="form-check-input" id="menu-pub-{{ m.id }}"
                          [checked]="m.isPublished"
                          (change)="togglePublished(m, $any($event.target).checked)" />
                        <label class="form-check-label visually-hidden" for="menu-pub-{{ m.id }}">
                          {{ 'menus.publish_label' | translate: { name: m.name } }}
                        </label>
                      </div>
                    </td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/menus', m.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        @if (!m.isSystem) {
                          <button
                            type="button"
                            class="action-btn action-btn-danger"
                            [title]="'common.delete' | translate"
                            [disabled]="deletingId() === m.id"
                            (click)="removeMenu(m)"
                          >
                            <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                          </button>
                        }
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="4">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'menus.empty' | translate }}</div>
                        <a routerLink="/menus/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'menus.create_first' | translate }}
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
export class AdminMenus {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly list = this.service.menusResource();
  protected readonly deletingId = signal<number | null>(null);

  protected togglePublished(menu: AdminMenuDto, isPublished: boolean): void {
    this.service.updateMenu(menu.id, { name: menu.name ?? '', isPublished }).subscribe({
      next: () => this.list.reload(),
      error: () => this.toast.error(this.translate.instant('menus.update_failed')),
    });
  }

  protected async removeMenu(menu: AdminMenuDto): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('menus.confirm_delete', { name: menu.name ?? '' }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.deletingId.set(menu.id);
    this.service.deleteMenu(menu.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('menus.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
