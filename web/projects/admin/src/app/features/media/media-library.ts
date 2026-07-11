import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AdminMediaService, type MediaListItemDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmService, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

const PAGE_SIZE = 24;

/**
 * Media library browser over `GET /api/admin/media`: debounced filename search,
 * multi-file upload (sequential `AdminMediaService.upload` calls so one bad file
 * doesn't abort the batch), a responsive thumbnail grid, and per-item copy-URL /
 * delete actions. Delete surfaces the backend's 409 (file still referenced by a
 * product/category/brand) as a friendly toast instead of a generic failure.
 */
@Component({
  selector: 'app-admin-media-library',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'media.title' | translate"
      [subtitle]="'media.subtitle' | translate"
    >
      <button
        type="button"
        class="btn btn-primary d-inline-flex align-items-center gap-1"
        [disabled]="uploading()"
        (click)="fileInput.click()"
      >
        <lib-icon name="plus" [size]="18" />
        {{ (uploading() ? 'media.uploading' : 'media.upload') | translate }}
      </button>
      <input
        #fileInput
        type="file"
        multiple
        hidden
        [disabled]="uploading()"
        (change)="onFilesSelected($event)"
      />
    </app-page-header>

    <div class="card border-0 shadow-sm">
      <div class="card-body">
        <div class="search-box mb-3" style="max-width: 360px">
          <span class="search-box-icon"><lib-icon name="search" [size]="17" /></span>
          <input
            type="search"
            class="form-control"
            [value]="term()"
            (input)="onSearchInput($event)"
            [placeholder]="'media.search_ph' | translate"
            [attr.aria-label]="'media.search_ph' | translate"
          />
        </div>

        @if (list.isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (list.error()) {
          <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
        } @else if (list.value(); as result) {
          @if ((result.items ?? []).length) {
            <div class="row row-cols-2 row-cols-sm-3 row-cols-md-4 row-cols-lg-5 g-3">
              @for (item of result.items ?? []; track item.id) {
                <div class="col">
                  <div class="card border-0 shadow-sm h-100">
                    <div
                      class="ratio ratio-1x1 bg-body-secondary rounded-top overflow-hidden"
                    >
                      @if (item.mediaType === 1) {
                        <img
                          [src]="item.url"
                          [alt]="item.fileName ?? ''"
                          class="w-100 h-100"
                          style="object-fit: cover"
                          loading="lazy"
                        />
                      } @else {
                        <div class="d-flex align-items-center justify-content-center h-100 text-body-secondary">
                          <lib-icon name="box" [size]="32" />
                        </div>
                      }
                    </div>
                    <div class="card-body p-2">
                      <div class="small text-truncate fw-medium" [title]="item.fileName ?? ''">
                        {{ item.fileName }}
                      </div>
                      <div class="d-flex justify-content-between align-items-center mb-2">
                        <span class="small text-body-secondary">{{ formatSize(item.fileSize) }}</span>
                        @if (item.referenceCount > 0) {
                          <span class="badge text-bg-light border">
                            {{ 'media.in_use' | translate: { count: item.referenceCount } }}
                          </span>
                        } @else {
                          <span class="badge text-bg-light border text-body-secondary">
                            {{ 'media.unused' | translate }}
                          </span>
                        }
                      </div>
                      <div class="d-flex gap-1">
                        <button
                          type="button"
                          class="btn btn-outline-secondary btn-sm flex-fill"
                          (click)="copyUrl(item.url)"
                        >
                          {{ 'media.copy_url' | translate }}
                        </button>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === item.id"
                          (click)="remove(item)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              }
            </div>
          } @else {
            <div class="empty-state">
              <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
              <div class="empty-title">{{ 'media.empty' | translate }}</div>
            </div>
          }

          @if ((result.items ?? []).length || page() > 1) {
            <div class="list-pager">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-1"
                [disabled]="page() === 1"
                (click)="prev()"
              >
                <lib-icon name="chevStart" [size]="15" />
                {{ 'common.previous' | translate }}
              </button>
              <span class="page-chip">
                {{ 'common.page_info' | translate: { page: page(), count: (result.items ?? []).length } }}
              </span>
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-1"
                [disabled]="!hasMore()"
                (click)="next()"
              >
                {{ 'common.next' | translate }}
                <lib-icon name="chevEnd" [size]="15" />
              </button>
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class AdminMediaLibrary {
  private readonly service = inject(AdminMediaService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly term = signal('');
  protected readonly page = signal(1);
  protected readonly uploading = signal(false);
  protected readonly deletingId = signal<number | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly list = this.service.listResource(() => ({
    page: this.page(),
    pageSize: PAGE_SIZE,
    search: this.term() || undefined,
  }));

  protected readonly hasMore = computed(() => {
    const result = this.list.value();
    if (!result) {
      return false;
    }
    return this.page() * PAGE_SIZE < result.totalCount;
  });

  /** Live search, debounced so we don't hit the API on every keystroke. */
  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => {
      this.term.set(value);
      this.page.set(1);
    }, 300);
  }

  protected prev(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  protected next(): void {
    if (this.hasMore()) {
      this.page.update((p) => p + 1);
    }
  }

  /** Uploads sequentially — a single failure is toasted but doesn't stop the rest of the batch. */
  protected async onFilesSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (!files.length) {
      return;
    }
    this.uploading.set(true);
    for (const file of files) {
      try {
        await firstValueFrom(this.service.upload(file));
      } catch {
        this.toast.error(this.translate.instant('media.upload_failed', { name: file.name }));
      }
    }
    this.uploading.set(false);
    input.value = '';
    this.list.reload();
  }

  protected copyUrl(url: string): void {
    void navigator.clipboard.writeText(url).then(
      () => this.toast.success(this.translate.instant('media.copy_ok')),
      () => this.toast.error(this.translate.instant('media.copy_failed')),
    );
  }

  protected async remove(item: MediaListItemDto): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('media.confirm_delete', { name: item.fileName ?? '#' + item.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.deletingId.set(item.id);
    this.service.delete(item.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('media.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.deletingId.set(null);
        if (err.status === 409) {
          this.toast.error(this.translate.instant('media.delete_referenced'));
        } else {
          this.toast.error(this.translate.instant('media.delete_failed'));
        }
      },
    });
  }

  protected formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
