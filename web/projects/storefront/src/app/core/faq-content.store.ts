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

  /**
   * The Q&A pairs to render, in server order (`q{n}`/`a{n}` in the `faq-list` section). Admins can
   * add questions from the CMS, so the list is data-driven rather than a fixed 1…6. Empty while the
   * resource loads (or if the API is unreachable); the page then falls back to the built-in copy.
   */
  readonly questions = computed(() => {
    const questionText = new Map<string, string | null>();
    const answerText = new Map<string, string | null>();
    const order: string[] = [];
    for (const block of this.resource.value() ?? []) {
      if (block.sectionKey !== 'faq-list') {
        continue;
      }
      const q = /^q(\d+)$/.exec(block.blockKey);
      if (q) {
        if (!questionText.has(q[1])) {
          order.push(q[1]);
        }
        questionText.set(q[1], block.value);
        continue;
      }
      const a = /^a(\d+)$/.exec(block.blockKey);
      if (a) {
        answerText.set(a[1], block.value);
      }
    }
    return order.map((index) => ({
      index,
      question: questionText.get(index) ?? '',
      answer: answerText.get(index) ?? '',
    }));
  });
}
