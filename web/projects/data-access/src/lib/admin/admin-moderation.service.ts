import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, type PagedResult, toQueryParams } from '../http-utils';
import type { AdminCommentDto, AdminReviewDto } from '../models';

/** Moderation list filter (status codes OR-ed; empty = all) with paging. */
export interface AdminModerationQuery {
  statuses?: number[];
  page?: number;
  pageSize?: number;
}

/** Review + comment moderation (`/api/admin/reviews`, `/api/admin/comments`). */
@Injectable({ providedIn: 'root' })
export class AdminModerationService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/reviews — paged envelope with total count. */
  reviewsResource(query: () => AdminModerationQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AdminReviewDto>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/reviews`,
          params: toQueryParams({ statuses: q.statuses, page: q.page, pageSize: q.pageSize }),
        };
      }),
    );
  }

  /** PUT /api/admin/reviews/{id}/status */
  setReviewStatus(id: number, status: number): Observable<void> {
    return this.http.put<void>(`${API_ROOT}/admin/reviews/${id}/status`, { status });
  }

  /** DELETE /api/admin/reviews/{id} */
  deleteReview(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/reviews/${id}`);
  }

  /** GET /api/admin/comments — paged envelope with total count. */
  commentsResource(query: () => AdminModerationQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AdminCommentDto>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/comments`,
          params: toQueryParams({ statuses: q.statuses, page: q.page, pageSize: q.pageSize }),
        };
      }),
    );
  }

  /** PUT /api/admin/comments/{id}/status */
  setCommentStatus(id: number, status: number): Observable<void> {
    return this.http.put<void>(`${API_ROOT}/admin/comments/${id}/status`, { status });
  }

  /** DELETE /api/admin/comments/{id} */
  deleteComment(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/comments/${id}`);
  }
}
