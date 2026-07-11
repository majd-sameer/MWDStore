import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type { MediaDto, MediaListResult } from '../models';

/** Admin media library (`/api/admin/media`). */
@Injectable({ providedIn: 'root' })
export class AdminMediaService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** POST /api/admin/media (multipart). Returns the stored media row + URL. */
  upload(file: File): Observable<MediaDto> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<MediaDto>(`${API_ROOT}/admin/media`, body);
  }

  /** GET /api/admin/media — paged library listing with reference counts. */
  listResource(query: () => { page?: number; pageSize?: number; search?: string | null }) {
    return runInInjectionContext(this.injector, () =>
      httpResource<MediaListResult>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/media`,
          params: toQueryParams({
            page: q.page,
            pageSize: q.pageSize,
            search: q.search,
          }),
        };
      }),
    );
  }

  /** DELETE /api/admin/media/{id} — 409 when the file is still referenced. */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/media/${id}`);
  }
}
