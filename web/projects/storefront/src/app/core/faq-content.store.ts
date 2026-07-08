import { computed, inject, Injectable } from '@angular/core';
import { ContentBlocksService, type ContentBlockDto } from 'data-access';

/**
 * Loads the FAQ page's editable content blocks once and exposes a `block(section, key)` lookup. The
 * page renders each Q&A over the block value, falling back to hard-coded (i18n) copy when a block is
 * absent, so it never renders empty if the API/seeder lags. Mirrors {@link AboutContentStore}.
 */
@Injectable({ providedIn: 'root' })
export class FaqContentStore {
  private readonly service = inject(ContentBlocksService);
  private readonly resource = this.service.pageResource(() => 'faq');

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
