import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { AdminModerationService, type AdminModerationQuery } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from 'core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';
import { FilterDropdown, type FilterOption, type FilterValue } from '../../shared/filter-dropdown';

const DEFAULT_PAGE_SIZE = 10;

const STATUS_KEYS: Record<number, string> = {
  1: 'moderation.status_pending',
  5: 'moderation.status_approved',
  8: 'moderation.status_not_approved',
};

/** Status filter option value/key pairs (1 = Pending, 5 = Approved, 8 = NotApproved). */
const STATUS_OPTIONS = [
  { value: 1, key: 'moderation.status_pending' },
  { value: 5, key: 'moderation.status_approved' },
  { value: 8, key: 'moderation.status_not_approved' },
];

/**
 * Review + comment moderation (old Reviews/Comments admin pages). Approving a
 * review also refreshes the product's denormalized rating fields server-side.
 */
@Component({
  selector: 'app-admin-moderation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    TranslatePipe,
    PageHeader,
    TableSkeleton,
    TableFooter,
    FilterDropdown,
  ],
  templateUrl: './moderation.html',
})
export class AdminModeration {
  private readonly service = inject(AdminModerationService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly tab = signal<'reviews' | 'comments'>('reviews');
  protected readonly statusFilter = signal<FilterValue[]>([]);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  /** Status filter options, re-labelled when the console language switches. */
  protected readonly statusFilterOptions = computed<FilterOption[]>(() => {
    this.language.lang();
    return STATUS_OPTIONS.map((o) => ({
      value: o.value,
      label: this.translate.instant(o.key),
    }));
  });

  private readonly query = computed<AdminModerationQuery>(() => ({
    statuses: this.statusFilter() as number[],
    page: this.page(),
    pageSize: this.pageSize(),
  }));

  protected readonly reviews = this.service.reviewsResource(this.query);
  protected readonly comments = this.service.commentsResource(this.query);

  protected readonly reviewRows = computed(() => this.reviews.value()?.items ?? []);
  protected readonly commentRows = computed(() => this.comments.value()?.items ?? []);
  protected readonly reviewsTotal = computed(() => this.reviews.value()?.total ?? 0);
  protected readonly commentsTotal = computed(() => this.comments.value()?.total ?? 0);

  protected setTab(tab: 'reviews' | 'comments'): void {
    this.tab.set(tab);
    this.page.set(1);
  }

  protected setStatuses(values: FilterValue[]): void {
    this.statusFilter.set(values);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  protected statusKey(status: number): string {
    return STATUS_KEYS[status] ?? 'moderation.status_unknown';
  }

  protected statusBadge(status: number): string {
    switch (status) {
      case 5:
        return 'badge text-bg-success';
      case 8:
        return 'badge text-bg-warning';
      default:
        return 'badge text-bg-secondary';
    }
  }

  protected setReviewStatus(id: number, status: number): void {
    this.service.setReviewStatus(id, status).subscribe({
      next: () => this.reviews.reload(),
      error: () => this.toast.error(this.translate.instant('moderation.review_update_failed')),
    });
  }

  protected deleteReview(id: number): void {
    if (!confirm(this.translate.instant('moderation.confirm_delete_review'))) {
      return;
    }
    this.service.deleteReview(id).subscribe({
      next: () => this.reviews.reload(),
      error: () => this.toast.error(this.translate.instant('moderation.review_delete_failed')),
    });
  }

  protected setCommentStatus(id: number, status: number): void {
    this.service.setCommentStatus(id, status).subscribe({
      next: () => this.comments.reload(),
      error: () => this.toast.error(this.translate.instant('moderation.comment_update_failed')),
    });
  }

  protected deleteComment(id: number): void {
    if (!confirm(this.translate.instant('moderation.confirm_delete_comment'))) {
      return;
    }
    this.service.deleteComment(id).subscribe({
      next: () => this.comments.reload(),
      error: () => this.toast.error(this.translate.instant('moderation.comment_delete_failed')),
    });
  }
}
