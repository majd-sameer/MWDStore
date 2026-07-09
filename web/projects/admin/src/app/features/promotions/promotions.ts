import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  AdminPromotionsService,
  type AdminCartRuleListItem,
  type AdminCartRuleUsageDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

const DEFAULT_PAGE_SIZE = 10;

/** Slices `items` to the given page, clamping the page to the last one. */
function paginate<T>(items: readonly T[], page: number, pageSize: number): T[] {
  const lastPage = Math.max(1, Math.ceil(items.length / pageSize));
  const current = Math.min(page, lastPage);
  return items.slice((current - 1) * pageSize, current * pageSize);
}

/**
 * Promotions browser: the cart-rule list plus a recent-usage log. Creating and
 * editing a promotion happen on their own page (`/promotions/new`,
 * `/promotions/:id`), mirroring the product list/form split.
 *
 * Both endpoints return the full list, so the search filters and pagination
 * below run client-side over the loaded rows.
 */
@Component({
  selector: 'app-admin-promotions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Icon, TranslatePipe, PageHeader, TableSkeleton, TableFooter],
  templateUrl: './promotions.html',
})
export class AdminPromotions {
  private readonly service = inject(AdminPromotionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly usages = signal<AdminCartRuleUsageDto[]>([]);
  protected readonly deletingId = signal<number | null>(null);

  // ----- Cart-rule list: search + pagination -----------------------------------
  protected readonly search = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  private readonly filtered = computed<AdminCartRuleListItem[]>(() => {
    const rows = this.list.value() ?? [];
    const term = this.search().trim().toLowerCase();
    return term
      ? rows.filter((r) => (r.name ?? '').toLowerCase().includes(term))
      : rows;
  });

  protected readonly total = computed(() => this.filtered().length);
  protected readonly rows = computed(() =>
    paginate(this.filtered(), this.page(), this.pageSize()),
  );

  // ----- Usage log: search + pagination ----------------------------------------
  protected readonly usageSearch = signal('');
  protected readonly usagePage = signal(1);
  protected readonly usagePageSize = signal(DEFAULT_PAGE_SIZE);

  private readonly filteredUsages = computed<AdminCartRuleUsageDto[]>(() => {
    const term = this.usageSearch().trim().toLowerCase();
    if (!term) {
      return this.usages();
    }
    return this.usages().filter((u) =>
      [u.cartRuleName, u.couponCode, u.userEmail].some((field) =>
        (field ?? '').toLowerCase().includes(term),
      ),
    );
  });

  protected readonly usagesTotal = computed(() => this.filteredUsages().length);
  protected readonly usageRows = computed(() =>
    paginate(this.filteredUsages(), this.usagePage(), this.usagePageSize()),
  );

  constructor() {
    this.service.usages().subscribe({
      next: (items) => this.usages.set(items),
      error: () => this.usages.set([]),
    });
  }

  protected setSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  protected setUsageSearch(value: string): void {
    this.usageSearch.set(value);
    this.usagePage.set(1);
  }

  protected setUsagePageSize(size: number): void {
    this.usagePageSize.set(size);
    this.usagePage.set(1);
  }

  protected remove(r: AdminCartRuleListItem): void {
    if (!confirm(this.translate.instant('promotions.confirm_delete', { name: r.name ?? '#' + r.id }))) {
      return;
    }
    this.deletingId.set(r.id);
    this.service.delete(r.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('promotions.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('promotions.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
