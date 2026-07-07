import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';

/** One editable content block (both languages) as returned to the admin editor. */
export interface AdminContentBlock {
  id: number;
  sectionKey: string;
  blockKey: string;
  type: string;
  valueAr: string | null;
  valueEn: string | null;
  mediumId: number | null;
  mediaUrl: string | null;
  linkUrl: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface AdminContentSection {
  sectionKey: string;
  blocks: AdminContentBlock[];
}

/** Editable fields only — keys and type are code-owned and cannot be changed. */
export interface ContentBlockUpdateRequest {
  value?: string | null;
  valueEn?: string | null;
  mediumId?: number | null;
  linkUrl?: string | null;
  isActive: boolean;
}

/** Admin CMS content blocks (`/api/admin/content-blocks`). */
@Injectable({ providedIn: 'root' })
export class AdminContentBlocksService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/content-blocks?page= (grouped by section). */
  listResource(page: () => string = () => 'home') {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminContentSection[]>(() => ({
        url: `${API_ROOT}/admin/content-blocks`,
        params: toQueryParams({ page: page() }),
      })),
    );
  }

  /** PUT /api/admin/content-blocks/{id} */
  update(id: number, body: ContentBlockUpdateRequest): Observable<AdminContentBlock> {
    return this.http.put<AdminContentBlock>(
      `${API_ROOT}/admin/content-blocks/${id}`,
      body,
    );
  }
}
