import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
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
  imports: [DatePipe, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'moderation.title' | translate"
      [subtitle]="'moderation.subtitle' | translate"
    />

    <ul class="nav nav-tabs mb-3">
      <li class="nav-item">
        <button type="button" class="nav-link" [class.active]="tab() === 'reviews'"
          (click)="tab.set('reviews')">
          {{ 'moderation.reviews_tab' | translate }}
        </button>
      </li>
      <li class="nav-item">
        <button type="button" class="nav-link" [class.active]="tab() === 'comments'"
          (click)="tab.set('comments')">
          {{ 'moderation.comments_tab' | translate }}
        </button>
      </li>
    </ul>

    <div class="d-flex gap-2 mb-3">
      <select class="form-select form-select-sm w-auto"
        (change)="statusFilter.set($any($event.target).value === '' ? null : +$any($event.target).value)">
        <option value="">{{ 'moderation.all_statuses' | translate }}</option>
        <option value="1">{{ 'moderation.status_pending' | translate }}</option>
        <option value="5">{{ 'moderation.status_approved' | translate }}</option>
        <option value="8">{{ 'moderation.status_not_approved' | translate }}</option>
      </select>
    </div>

    @if (tab() === 'reviews') {
      <div class="card border-0 shadow-sm">
        <div class="card-body">
          @if (reviews.isLoading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
              </div>
            </div>
          } @else {
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th>{{ 'moderation.col_review' | translate }}</th>
                  <th>{{ 'moderation.col_product' | translate }}</th>
                  <th>{{ 'moderation.col_rating' | translate }}</th>
                  <th>{{ 'moderation.col_by' | translate }}</th>
                  <th>{{ 'common.status' | translate }}</th>
                  <th>{{ 'common.when' | translate }}</th>
                  <th class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (r of reviews.value() ?? []; track r.id) {
                  <tr>
                    <td style="max-width: 22rem">
                      <span class="fw-medium">{{ r.title }}</span>
                      <div class="small text-body-secondary text-truncate">{{ r.comment }}</div>
                    </td>
                    <td>{{ r.productName ?? ('#' + r.entityId) }}</td>
                    <td>{{ r.rating }}/5</td>
                    <td>{{ r.reviewerName || r.userEmail }}</td>
                    <td><span class="badge" [class]="statusBadge(r.status)">{{ statusKey(r.status) | translate }}</span></td>
                    <td class="small">{{ r.createdOn | date: 'mediumDate' }}</td>
                    <td class="text-end text-nowrap">
                      @if (r.status !== 5) {
                        <button type="button" class="btn btn-sm btn-outline-success"
                          (click)="setReviewStatus(r.id, 5)">{{ 'moderation.approve' | translate }}</button>
                      }
                      @if (r.status !== 8) {
                        <button type="button" class="btn btn-sm btn-outline-warning ms-1"
                          (click)="setReviewStatus(r.id, 8)">{{ 'moderation.reject' | translate }}</button>
                      }
                      <button type="button" class="btn btn-sm btn-outline-danger ms-1"
                        (click)="deleteReview(r.id)">{{ 'common.delete' | translate }}</button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="text-center text-body-secondary py-4">
                      {{ 'moderation.no_reviews' | translate }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    } @else {
      <div class="card border-0 shadow-sm">
        <div class="card-body">
          @if (comments.isLoading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
              </div>
            </div>
          } @else {
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th>{{ 'moderation.col_comment' | translate }}</th>
                  <th>{{ 'moderation.col_by' | translate }}</th>
                  <th>{{ 'common.status' | translate }}</th>
                  <th>{{ 'common.when' | translate }}</th>
                  <th class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (c of comments.value() ?? []; track c.id) {
                  <tr>
                    <td style="max-width: 28rem">
                      <div class="text-truncate">{{ c.commentText }}</div>
                      @if (c.parentId) {
                        <span class="badge text-bg-light border">{{ 'moderation.reply' | translate }}</span>
                      }
                    </td>
                    <td>{{ c.commenterName || c.userEmail }}</td>
                    <td><span class="badge" [class]="statusBadge(c.status)">{{ statusKey(c.status) | translate }}</span></td>
                    <td class="small">{{ c.createdOn | date: 'mediumDate' }}</td>
                    <td class="text-end text-nowrap">
                      @if (c.status !== 5) {
                        <button type="button" class="btn btn-sm btn-outline-success"
                          (click)="setCommentStatus(c.id, 5)">{{ 'moderation.approve' | translate }}</button>
                      }
                      @if (c.status !== 8) {
                        <button type="button" class="btn btn-sm btn-outline-warning ms-1"
                          (click)="setCommentStatus(c.id, 8)">{{ 'moderation.reject' | translate }}</button>
                      }
                      <button type="button" class="btn btn-sm btn-outline-danger ms-1"
                        (click)="deleteComment(c.id)">{{ 'common.delete' | translate }}</button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="text-center text-body-secondary py-4">
                      {{ 'moderation.no_comments' | translate }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    }
  `,
})
export class AdminModeration {
  private readonly service = inject(AdminModerationService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly tab = signal<'reviews' | 'comments'>('reviews');
  protected readonly statusFilter = signal<number | null>(null);

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
