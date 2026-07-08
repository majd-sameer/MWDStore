import { TestBed } from '@angular/core/testing';
import { ContentBlocksService } from 'data-access';
import { AboutContentStore } from './about-content.store';

const heroTitle = {
  sectionKey: 'about-hero',
  blockKey: 'title',
  type: 'text',
  value: 'Hi',
  mediaUrl: null,
  linkUrl: null,
};

/** Builds a store over a fake resource yielding `blocks` (no HTTP). */
function storeWith(blocks: unknown[]): AboutContentStore {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: ContentBlocksService,
        useValue: { pageResource: () => ({ value: () => blocks }) },
      },
    ],
  });
  return TestBed.runInInjectionContext(() => new AboutContentStore());
}

describe('AboutContentStore block() fallback helper', () => {
  it('returns the matching block', () => {
    const store = storeWith([heroTitle]);
    expect(store.block('about-hero', 'title')?.value).toBe('Hi');
  });

  it('returns undefined for a missing block so callers fall back to their copy', () => {
    const store = storeWith([heroTitle]);
    expect(store.block('about-hero', 'missing')).toBeUndefined();
    expect(store.block('about-values', 'title')).toBeUndefined();
  });

  it('returns undefined while the resource is empty/loading', () => {
    const store = storeWith([]);
    expect(store.block('about-hero', 'title')).toBeUndefined();
  });
});
