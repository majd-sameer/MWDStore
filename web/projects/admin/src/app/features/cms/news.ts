import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  AdminCmsService,
  type AdminNewsItemListItem,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * News browser: the article list with a news-category manager alongside.
 * Creating and editing an article happen on their own page (`/news/new`,
 * `/news/:id`); categories stay here as a secondary entity.
 */
@Component({
  selector: 'app-admin-news',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Button, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'news.title' | translate"
      [subtitle]="'news.subtitle' | translate"
    >
      <a routerLink="/news/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'news.new' | translate }}
      </a>
    </app-page-header>

    <div class="row g-4">
      <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
          <div class="card-body">
            @if (items.isLoading()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
                </div>
              </div>
            } @else if (items.error()) {
              <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
            } @else if (items.value(); as rows) {
              <div class="table-responsive">
                <table class="table table-hover align-middle mb-0">
                  <thead>
                    <tr>
                      <th scope="col">{{ 'news.col_article' | translate }}</th>
                      <th scope="col">{{ 'common.status' | translate }}</th>
                      <th scope="col">{{ 'common.created' | translate }}</th>
                      <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (n of rows; track n.id) {
                      <tr>
                        <td>
                          <div class="d-flex align-items-center gap-2">
                            @if (n.thumbnailUrl) {
                              <img [src]="n.thumbnailUrl" alt="" class="rounded border"
                                style="width: 40px; height: 40px; object-fit: cover" />
                            }
                            <div>
                              <a [routerLink]="['/news', n.id]" class="text-decoration-none fw-medium">{{ n.name }}</a>
                              <div class="small text-body-secondary">/{{ n.slug }}</div>
                            </div>
                          </div>
                        </td>
                        <td>
                          @if (n.isPublished) {
                            <span class="badge text-bg-success">{{ 'common.published' | translate }}</span>
                          } @else {
                            <span class="badge text-bg-secondary">{{ 'common.draft' | translate }}</span>
                          }
                        </td>
                        <td class="small">{{ n.createdOn | date: 'mediumDate' }}</td>
                        <td class="text-end">
                          <span class="d-inline-flex gap-1">
                            <a [routerLink]="['/news', n.id]" class="action-btn" [title]="'common.edit' | translate">
                              <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                            </a>
                            <button
                              type="button"
                              class="action-btn action-btn-danger"
                              [title]="'common.delete' | translate"
                              [disabled]="deletingId() === n.id"
                              (click)="remove(n)"
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
                            <div class="empty-title">{{ 'news.empty' | translate }}</div>
                            <a routerLink="/news/new" class="btn btn-primary btn-sm mt-2">
                              {{ 'news.create_first' | translate }}
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
      </div>

      <div class="col-lg-4">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'news.categories_title' | translate }}</div>
          <div class="card-body">
            @for (c of categories.value() ?? []; track c.id) {
              <div class="d-flex align-items-center gap-2 mb-2">
                <input type="text" class="form-control form-control-sm" [value]="c.name"
                  (change)="renameCategory(c.id, $any($event.target).value)" />
                <button type="button" class="btn btn-sm btn-outline-danger"
                  (click)="removeCategory(c.id, c.name)">✕</button>
              </div>
            } @empty {
              <p class="text-body-secondary small">{{ 'news.no_categories' | translate }}</p>
            }
            <div class="d-flex gap-2 mt-3">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'news.new_category_ph' | translate" #catName />
              <button type="button" libButton variant="secondary" [outline]="true"
                (click)="addCategory(catName)">
                {{ 'common.add' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminNews {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly items = this.service.newsItemsResource();
  protected readonly categories = this.service.newsCategoriesResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(n: AdminNewsItemListItem): void {
    if (!confirm(this.translate.instant('news.confirm_delete', { name: n.name ?? '#' + n.id }))) {
      return;
    }
    this.deletingId.set(n.id);
    this.service.deleteNewsItem(n.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('news.deleted_ok'));
        this.deletingId.set(null);
        this.items.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('news.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }

  // ----- Categories ------------------------------------------------------------

  protected addCategory(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createNewsCategory({ name, isPublished: true }).subscribe({
      next: () => {
        input.value = '';
        this.categories.reload();
      },
      error: () => this.toast.error(this.translate.instant('news.category_create_failed')),
    });
  }

  protected renameCategory(id: number, name: string): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    this.service.updateNewsCategory(id, { name: trimmed, isPublished: true }).subscribe({
      next: () => this.toast.success(this.translate.instant('news.category_updated')),
      error: () => this.toast.error(this.translate.instant('news.category_update_failed')),
    });
  }

  protected removeCategory(id: number, name: string | null): void {
    if (!confirm(this.translate.instant('news.confirm_delete_category', { name: name ?? '' }))) {
      return;
    }
    this.service.deleteNewsCategory(id).subscribe({
      next: () => this.categories.reload(),
      error: () => this.toast.error(this.translate.instant('news.category_delete_failed')),
    });
  }
}
