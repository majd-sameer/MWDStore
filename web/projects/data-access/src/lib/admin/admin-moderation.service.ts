import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type { AdminCommentDto, AdminReviewDto } from '../models';

/** Review + comment moderation (`/api/admin/reviews`, `/api/admin/comments`). */
@Injectable({ providedIn: 'root' })
export class AdminModerationService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/reviews */
  reviewsResource(status: () => number | null = () => null) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminReviewDto[]>(() => ({
        url: `${API_ROOT}/admin/reviews`,
        params: toQueryParams({ status: status() }),
      })),
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

  /** GET /api/admin/comments */
  commentsResource(status: () => number | null = () => null) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCommentDto[]>(() => ({
        url: `${API_ROOT}/admin/comments`,
        params: toQueryParams({ status: status() }),
      })),
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
