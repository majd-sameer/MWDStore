import { TestBed } from '@angular/core/testing';
import { AuthService, LanguageService } from 'core';
import { AdminLayout } from './admin-layout';

interface VisibleSection {
  key: string | null;
  items: readonly { path: string }[];
}

function layoutForRoles(roles: readonly string[]): AdminLayout {
  TestBed.resetTestingModule();
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

function visibleSections(layout: AdminLayout): VisibleSection[] {
  return (layout as unknown as { visibleSections: () => VisibleSection[] }).visibleSections();
}

function sectionKeys(layout: AdminLayout): (string | null)[] {
  return visibleSections(layout).map((section) => section.key);
}

function itemPaths(layout: AdminLayout): string[] {
  return visibleSections(layout).flatMap((section) => section.items.map((item) => item.path));
}

describe('AdminLayout sidebar visibility', () => {
  it('shows every business section for super-admin', () => {
    const keys = sectionKeys(layoutForRoles(['super-admin']));
    for (const section of ['stock', 'content', 'sales', 'system']) {
      expect(keys).toContain(section);
    }
  });

  it('shows the dev-assistant link to super-admin only', () => {
    expect(itemPaths(layoutForRoles(['super-admin']))).toContain('/dev-assistant');
    expect(itemPaths(layoutForRoles(['admin']))).not.toContain('/dev-assistant');
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
