import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
import {
  AdminBrandsService,
  AdminCategoriesService,
  AdminProductsService,
  type AdminProductQuery,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { StatusPill } from '../../shared/status-pill';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

const DEFAULT_PAGE_SIZE = 20;

/** Publish-state segments for the filter chips. */
type StatusFilter = 'all' | 'published' | 'draft' | 'deleted';

/**
 * Admin product browser over the paged `GET /api/admin/products` envelope:
 * debounced name search, one-click publish-state chips (all / published / draft
 * / deleted) and brand + category selects — all server-side filters — plus
 * numbered pagination with total count and icon row actions to edit or
 * soft-delete.
 */
@Component({
  selector: 'app-admin-product-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MoneyPipe,
    FormsModule,
    RouterLink,
    NgSelectModule,
    Icon,
    TranslatePipe,
    PageHeader,
    StatusPill,
    TableSkeleton,
    TableFooter,
  ],
  templateUrl: './product-list.html',
})
export class AdminProductList {
  private readonly service = inject(AdminProductsService);
  private readonly brandsService = inject(AdminBrandsService);
  private readonly categoriesService = inject(AdminCategoriesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly term = signal('');
  protected readonly status = signal<StatusFilter>('all');
  protected readonly signatureOnly = signal(false);
  protected readonly brandId = signal<number | null>(null);
  protected readonly categoryId = signal<number | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  protected readonly deletingId = signal<number | null>(null);

  protected readonly brands = this.brandsService.listResource(() => false);
  protected readonly categories = this.categoriesService.listResource(() => false);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly query = computed<AdminProductQuery>(() => {
    const status = this.status();
    return {
      query: this.term() || undefined,
      deletedOnly: status === 'deleted' || undefined,
      isPublished:
        status === 'published' ? true : status === 'draft' ? false : undefined,
      isSignature: this.signatureOnly() || undefined,
      brandId: this.brandId() ?? undefined,
      categoryId: this.categoryId() ?? undefined,
      page: this.page(),
      pageSize: this.pageSize(),
    };
  });

  protected readonly products = this.service.listResource(this.query);

  protected readonly rows = computed(() => this.products.value()?.items ?? []);
  protected readonly total = computed(() => this.products.value()?.total ?? 0);

  protected readonly hasFilters = computed(
    () =>
      Boolean(this.term()) ||
      this.status() !== 'all' ||
      this.signatureOnly() ||
      this.brandId() !== null ||
      this.categoryId() !== null,
  );

  private readonly brandsById = computed(
    () => new Map((this.brands.value() ?? []).map((b) => [b.id, b.name])),
  );

  protected brandName(id: number | null): string {
    return (id !== null ? this.brandsById().get(id) : null) ?? '—';
  }

  /** Products whose thumbnail file 404'd — show the icon tile instead. */
  protected readonly thumbFailed = signal<ReadonlySet<number>>(new Set());

  protected onThumbError(id: number): void {
    this.thumbFailed.update((s) => new Set(s).add(id));
  }

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

  protected setStatus(status: StatusFilter): void {
    this.status.set(status);
    this.page.set(1);
  }

  protected toggleSignatureFilter(): void {
    this.signatureOnly.update((v) => !v);
    this.page.set(1);
  }

  /** Quick list-page toggle of the Signature flag (audited server-side as an Update). */
  protected toggleSignature(id: number, current: boolean): void {
    this.service.setSignature(id, { isSignature: !current }).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('products.signature_updated'));
        this.products.reload();
      },
      error: () => this.toast.error(this.translate.instant('products.signature_failed')),
    });
  }

  protected setBrand(id: number | null): void {
    this.brandId.set(id);
    this.page.set(1);
  }

  protected setCategory(id: number | null): void {
    this.categoryId.set(id);
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.term.set('');
    this.status.set('all');
    this.signatureOnly.set(false);
    this.brandId.set(null);
    this.categoryId.set(null);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  protected remove(id: number, name: string | null): void {
    if (!confirm(this.translate.instant('products.confirm_delete', { name: name ?? '#' + id }))) {
      return;
    }
    this.deletingId.set(id);
    this.service.delete(id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('products.deleted_ok'));
        this.deletingId.set(null);
        this.products.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('products.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
