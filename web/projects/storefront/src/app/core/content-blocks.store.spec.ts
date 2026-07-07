import { TestBed } from '@angular/core/testing';
import { ContentBlocksService } from 'data-access';
import { ContentBlocksStore } from './content-blocks.store';

const heroTitle = {
  sectionKey: 'hero-grid',
  blockKey: 'hero-copy.title',
  type: 'text',
  value: 'Hi',
  mediaUrl: null,
  linkUrl: null,
};

/** Builds a store over a fake resource yielding `blocks` (no HTTP). */
function storeWith(blocks: unknown[]): ContentBlocksStore {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: ContentBlocksService,
        useValue: { pageResource: () => ({ value: () => blocks }) },
      },
    ],
  });
  return TestBed.runInInjectionContext(() => new ContentBlocksStore());
}

describe('ContentBlocksStore block() fallback helper', () => {
  it('returns the matching block', () => {
    const store = storeWith([heroTitle]);
    expect(store.block('hero-grid', 'hero-copy.title')?.value).toBe('Hi');
  });

  it('returns undefined for a missing block so callers fall back to their copy', () => {
    const store = storeWith([heroTitle]);
    expect(store.block('hero-grid', 'missing')).toBeUndefined();
    expect(store.block('other-section', 'hero-copy.title')).toBeUndefined();
  });

  it('returns undefined while the resource is empty/loading', () => {
    const store = storeWith([]);
    expect(store.block('hero-grid', 'hero-copy.title')).toBeUndefined();
  });
});
