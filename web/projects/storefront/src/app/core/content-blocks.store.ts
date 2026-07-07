import { computed, inject, Injectable } from '@angular/core';
import { ContentBlocksService, type ContentBlockDto } from 'data-access';

/**
 * Loads the home page's editable content blocks once and exposes a `block(section, key)` lookup.
 * Sections read it and fall back to their hard-coded (i18n) copy when a block is absent, so the page
 * never renders empty if the API or seeder lags. The resource is created on first injection (when the
 * home page renders) and settles during SSR.
 */
@Injectable({ providedIn: 'root' })
export class ContentBlocksStore {
  private readonly service = inject(ContentBlocksService);
  private readonly resource = this.service.pageResource(() => 'home');

  private readonly byKey = computed(() => {
    const map = new Map<string, ContentBlockDto>();
    for (const block of this.resource.value() ?? []) {
      map.set(`${block.sectionKey}::${block.blockKey}`, block);
    }
    return map;
  });

  block(section: string, key: string): ContentBlockDto | undefined {
    return this.byKey().get(`${section}::${key}`);
  }
}
