import { computed, inject, Injectable } from '@angular/core';
import { ContentBlocksService, type ContentBlockDto } from 'data-access';

/**
 * Loads the About page's editable content blocks once and exposes a `block(section, key)` lookup.
 * The template reads it and falls back to its hard-coded (i18n) copy when a block is absent, so the
 * page never renders empty if the API or seeder lags. Mirrors {@link ContentBlocksStore} (home);
 * scoped to the `about` page so both resources settle independently during SSR.
 */
@Injectable({ providedIn: 'root' })
export class AboutContentStore {
  private readonly service = inject(ContentBlocksService);
  private readonly resource = this.service.pageResource(() => 'about');

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
