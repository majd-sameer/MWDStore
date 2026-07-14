import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import { LocaleState } from '../locale-state';
import type {
  DevAssistantCapabilities,
  DevAssistantQueryRequest,
  DevAssistantReply,
} from '../models';

/**
 * Developer Assistant portal (`/api/admin/dev-assistant`). Answers are composed server-side in the
 * active language: the capabilities resource sends `culture` (and thereby re-fetches on language
 * switch, like every localized resource), and queries carry it in the body.
 */
@Injectable({ providedIn: 'root' })
export class AdminDevAssistantService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly locale = inject(LocaleState);

  /** GET /api/admin/dev-assistant/capabilities — re-fetches when the language changes. */
  capabilitiesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<DevAssistantCapabilities>(() => ({
        url: `${API_ROOT}/admin/dev-assistant/capabilities`,
        params: { culture: this.locale.language() },
      })),
    );
  }

  /** POST /api/admin/dev-assistant/query */
  query(body: DevAssistantQueryRequest): Observable<DevAssistantReply> {
    return this.http.post<DevAssistantReply>(
      `${API_ROOT}/admin/dev-assistant/query?culture=${this.locale.language()}`,
      body,
    );
  }
}
