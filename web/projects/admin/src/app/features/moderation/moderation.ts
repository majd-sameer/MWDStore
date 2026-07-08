import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { AdminModerationService } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

const STATUS_KEYS: Record<number, string> = {
  1: 'moderation.status_pending',
  5: 'moderation.status_approved',
  8: 'moderation.status_not_approved',
};

/**
 * Review + comment moderation (old Reviews/Comments admin pages). Approving a
 * review also refreshes the product's denormalized rating fields server-side.
 */
@Component({
  selector: 'app-admin-moderation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, NgSelectModule, TranslatePipe, PageHeader],
  templateUrl: './moderation.html',
})
export class AdminModeration {
  private readonly service = inject(AdminModerationService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly tab = signal<'reviews' | 'comments'>('reviews');
  protected readonly statusFilter = signal<number | null>(null);

  /** Status filter options for the ng-select above the table. */
  protected readonly statusOptions = [
    { value: 1, key: 'moderation.status_pending' },
    { value: 5, key: 'moderation.status_approved' },
    { value: 8, key: 'moderation.status_not_approved' },
  ];

  protected readonly reviews = this.service.reviewsResource(() => this.statusFilter());
  protected readonly comments = this.service.commentsResource(() => this.statusFilter());

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
