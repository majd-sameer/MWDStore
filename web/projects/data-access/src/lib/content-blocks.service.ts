import { httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import { API_ROOT } from './http-utils';
import { LocaleState } from './locale-state';

/** One active content block (value already culture-overlaid; image blocks carry a resolved URL). */
export interface ContentBlockDto {
  sectionKey: string;
  blockKey: string;
  type: string;
  value: string | null;
  mediaUrl: string | null;
  linkUrl: string | null;
}

/**
 * Reads editable storefront content blocks (`GET /api/content/blocks/{pageKey}`). The resource
 * re-fetches when the language changes, so the culture-overlaid value follows the active language —
 * exactly like the catalog/news resources.
 */
@Injectable({ providedIn: 'root' })
export class ContentBlocksService {
  private readonly injector = inject(Injector);
  private readonly locale = inject(LocaleState);

  pageResource(pageKey: () => string) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ContentBlockDto[]>(() => ({
        url: `${API_ROOT}/content/blocks/${pageKey()}`,
        params: { culture: this.locale.language() },
      })),
    );
  }
}
