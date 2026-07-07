import { TestBed } from '@angular/core/testing';
import { AuthService, LanguageService } from 'core';
import { AdminLayout } from './admin-layout';

interface VisibleSection {
  key: string | null;
  items: readonly { path: string }[];
}

/** Builds an AdminLayout with a fake auth granting exactly `roles` — no template render needed. */
function layoutForRoles(roles: readonly string[]): AdminLayout {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: AuthService,
        useValue: {
          hasAnyRole: (candidate: readonly string[]) => candidate.some((r) => roles.includes(r)),
          fullName: () => '',
          email: () => '',
          logout: () => {},
        },
      },
      { provide: LanguageService, useValue: { lang: () => 'en', toggle: () => {} } },
    ],
  });
  return TestBed.runInInjectionContext(() => new AdminLayout());
}

function sectionKeys(layout: AdminLayout): (string | null)[] {
  return (layout as unknown as { visibleSections: () => VisibleSection[] })
    .visibleSections()
    .map((section) => section.key);
}

describe('AdminLayout sidebar visibility', () => {
  it('shows all five business sections for super-admin', () => {
    const keys = sectionKeys(layoutForRoles(['super-admin']));
    for (const section of ['stock', 'content', 'sales', 'people', 'system']) {
      expect(keys).toContain(section);
    }
  });

  it('shows only Stock management for a warehouse-keeper', () => {
    const keys = sectionKeys(layoutForRoles(['warehouse-keeper'])).filter(Boolean);
    expect(keys).toEqual(['stock']);
  });

  it('shows only Content management for a content-writer', () => {
    const keys = sectionKeys(layoutForRoles(['content-writer'])).filter(Boolean);
    expect(keys).toEqual(['content']);
  });

  it('drops sections with no reachable links', () => {
    const keys = sectionKeys(layoutForRoles(['sales']));
    expect(keys).toContain('sales'); // orders + customers are reachable
    expect(keys).not.toContain('stock');
    expect(keys).not.toContain('system');
  });
});
