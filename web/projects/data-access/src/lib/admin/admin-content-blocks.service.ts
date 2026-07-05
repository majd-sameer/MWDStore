import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type { AdminContentBlockDto, ContentBlockUpdateRequest } from '../models';

/** Admin read/update for the fixed set of homepage content blocks. No create/delete — blocks are
 * seeded once and only edited thereafter. */
@Injectable({ providedIn: 'root' })
export class AdminContentBlocksService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/content-blocks */
  listResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminContentBlockDto[]>(() => `${API_ROOT}/admin/content-blocks`),
    );
  }

  /** GET /api/admin/content-blocks/{id} */
  getResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminContentBlockDto>(() => `${API_ROOT}/admin/content-blocks/${id()}`),
    );
  }

  update(id: number, body: ContentBlockUpdateRequest): Observable<AdminContentBlockDto> {
    return this.http.put<AdminContentBlockDto>(`${API_ROOT}/admin/content-blocks/${id}`, body);
  }
}
