import { httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import { API_ROOT, toQueryParams } from './http-utils';
import { LocaleState } from './locale-state';
import type { ContentBlockDto } from './models';

/**
 * Storefront reads for admin-editable homepage content blocks
 * (`GET /api/content/blocks?prefix=...`). Sections look the result up by `key` and fall back to
 * their built-in i18n copy when a block is missing/unpublished — see the `home` feature.
 *
 * @example
 * private readonly content = inject(ContentService);
 * protected readonly homeBlocks = this.content.blocksResource(() => 'home');
 * // template: homeBlocks.value()?.find(b => b.key === 'home.hero')
 */
@Injectable({ providedIn: 'root' })
export class ContentService {
  private readonly injector = inject(Injector);
  private readonly locale = inject(LocaleState);

  /** GET /api/content/blocks */
  blocksResource(prefix: () => string | undefined = () => undefined) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ContentBlockDto[]>(() => ({
        url: `${API_ROOT}/content/blocks`,
        // `culture` isn't read by the backend (it resolves culture from Accept-Language), but
        // including it makes the resource refetch — and thus re-localize — on language switch.
        params: { ...toQueryParams({ prefix: prefix() }), culture: this.locale.language() },
      })),
    );
  }
}
