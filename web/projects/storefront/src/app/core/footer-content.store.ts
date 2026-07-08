import { computed, inject, Injectable } from '@angular/core';
import { ContentBlocksService, type ContentBlockDto } from 'data-access';

/**
 * Loads the footer's editable content blocks once and exposes a `block(section, key)` lookup. The
 * footer renders on every page (it lives in the layout), so this global store fetches the `footer`
 * page's blocks a single time. Text blocks fall back to hard-coded (i18n) copy when absent; the
 * `footer-social` link blocks only render when an admin has set a URL. Mirrors {@link AboutContentStore}.
 */
@Injectable({ providedIn: 'root' })
export class FooterContentStore {
  private readonly service = inject(ContentBlocksService);
  private readonly resource = this.service.pageResource(() => 'footer');

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
