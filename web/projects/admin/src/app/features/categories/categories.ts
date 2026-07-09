import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

const DEFAULT_PAGE_SIZE = 10;

/** Publish-state segments for the status filter chips. */
type StatusFilter = 'all' | 'published' | 'hidden';

/**
 * Category browser: a full-width list with publish status and display order.
 * Creating and editing happen on their own page (`/categories/new`,
 * `/categories/:id`), mirroring the product list/form split.
 *
 * The endpoint returns the full list, so the name search and status filter
 * below run client-side over the loaded rows.
 */
@Component({
  selector: 'app-admin-categories',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton, TableFooter],
  templateUrl: './categories.html',
})
export class AdminCategories {
  private readonly service = inject(AdminCategoriesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource(() => true);
  protected readonly deletingId = signal<number | null>(null);

  // ----- Filters + pagination (client-side) ------------------------------------
  protected readonly search = signal('');
  protected readonly status = signal<StatusFilter>('all');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  protected readonly filtered = computed<AdminCategoryDto[]>(() => {
    const rows = this.list.value() ?? [];
    const term = this.search().trim().toLowerCase();
    const status = this.status();
    return rows.filter((c) => {
      if (status === 'published' && !c.isPublished) {
        return false;
      }
      if (status === 'hidden' && c.isPublished) {
        return false;
      }
      if (!term) {
        return true;
      }
      return (
        (c.name ?? '').toLowerCase().includes(term) ||
        (c.slug ?? '').toLowerCase().includes(term)
      );
    });
  });

  protected readonly total = computed(() => this.filtered().length);

  /** The current page of filtered rows, clamping the page to the last one. */
  protected readonly rows = computed<AdminCategoryDto[]>(() => {
    const items = this.filtered();
    const size = this.pageSize();
    const lastPage = Math.max(1, Math.ceil(items.length / size));
    const current = Math.min(this.page(), lastPage);
    return items.slice((current - 1) * size, current * size);
  });

  protected readonly hasFilters = computed(
    () => Boolean(this.search()) || this.status() !== 'all',
  );

  protected setSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  protected setStatus(status: StatusFilter): void {
    this.status.set(status);
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.search.set('');
    this.status.set('all');
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

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
