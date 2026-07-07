import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService, LanguageService } from 'core';
import { AREA } from '../core/roles';

interface NavItem {
  readonly path: string;
  /** Translation key under `nav.` (also used as the track id). */
  readonly key: string;
  /** Bootstrap Icons class, e.g. `bi-box-seam`. */
  readonly icon: string;
  /** Roles allowed to see this link (mirrors the route's `roleGuard`). */
  readonly roles: readonly string[];
  /** `true` to highlight only on the exact path (the dashboard root). */
  readonly exact?: boolean;
}

interface NavSection {
  /** Translation key under `nav.` for the section label; null = no label. */
  readonly key: string | null;
  readonly items: readonly NavItem[];
}

/**
 * Authenticated admin chrome: a fixed navy sidebar with the feature links
 * grouped into the five business sections (Stock management / Content management
 * / Sales / People / System) plus a
 * topbar with a language toggle (en ⇄ ar via the shared core LanguageService),
 * the signed-in user and a sign-out action. All copy keyed through
 * ngx-translate; positioning uses logical properties so RTL mirrors cleanly.
 */
@Component({
  selector: 'app-admin-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe],
  styleUrl: './admin-layout.scss',
  templateUrl: './admin-layout.html',
})
export class AdminLayout {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);

  protected readonly initials = computed(() => {
    const source = this.auth.fullName() || this.auth.email() || '';
    const parts = source.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return source.slice(0, 2).toUpperCase() || '·';
  });

  private readonly sections: readonly NavSection[] = [
    {
      key: null,
      items: [
        { path: '/dashboard', key: 'dashboard', icon: 'bi-grid-1x2', roles: AREA.reports },
      ],
    },
    // Stock management — inventory ∪ fulfillment.
    {
      key: 'stock',
      items: [
        { path: '/inventory', key: 'inventory', icon: 'bi-clipboard-data', roles: AREA.inventory },
        { path: '/stock-out', key: 'stockOut', icon: 'bi-box-arrow-up', roles: AREA.inventory },
        { path: '/stock-out-log', key: 'stockOutLog', icon: 'bi-journal-arrow-up', roles: AREA.inventory },
        { path: '/warehouses', key: 'warehouses', icon: 'bi-building', roles: AREA.inventory },
        { path: '/shipping', key: 'shipping', icon: 'bi-truck', roles: AREA.fulfillment },
      ],
    },
    // Content management — catalog ∪ content. Phase 5 adds content-blocks here.
    {
      key: 'content',
      items: [
        { path: '/products', key: 'products', icon: 'bi-box-seam', roles: AREA.catalog },
        { path: '/categories', key: 'categories', icon: 'bi-folder2', roles: AREA.catalog },
        { path: '/brands', key: 'brands', icon: 'bi-tag', roles: AREA.catalog },
        { path: '/product-options', key: 'options', icon: 'bi-sliders', roles: AREA.catalog },
        { path: '/product-attributes', key: 'attributes', icon: 'bi-list-check', roles: AREA.catalog },
        { path: '/product-templates', key: 'templates', icon: 'bi-clipboard', roles: AREA.catalog },
        { path: '/news', key: 'news', icon: 'bi-newspaper', roles: AREA.content },
        { path: '/moderation', key: 'moderation', icon: 'bi-shield-check', roles: AREA.moderation },
      ],
    },
    // Sales — sales ∪ marketing ∪ reports.
    {
      key: 'sales',
      items: [
        { path: '/orders', key: 'orders', icon: 'bi-receipt', roles: AREA.sales },
        { path: '/customers', key: 'customers', icon: 'bi-people', roles: AREA.sales },
        { path: '/promotions', key: 'promotions', icon: 'bi-ticket-perforated', roles: AREA.marketing },
        { path: '/taxes', key: 'taxes', icon: 'bi-percent', roles: AREA.marketing },
        { path: '/payments', key: 'payments', icon: 'bi-credit-card', roles: AREA.settings },
        { path: '/vendors', key: 'vendors', icon: 'bi-shop', roles: AREA.settings },
      ],
    },
    // People — users (staff + roles) / customers (same route as Sales, intentionally duplicated).
    {
      key: 'people',
      items: [
        { path: '/users', key: 'users', icon: 'bi-person-badge', roles: AREA.users },
        { path: '/customers', key: 'customers', icon: 'bi-people', roles: AREA.sales },
      ],
    },
    // System — settings.
    {
      key: 'system',
      items: [
        { path: '/locations', key: 'countries', icon: 'bi-globe2', roles: AREA.settings },
        { path: '/audit-log', key: 'auditLog', icon: 'bi-clipboard-check', roles: AREA.settings },
        { path: '/settings', key: 'settings', icon: 'bi-gear', roles: AREA.settings },
      ],
    },
  ];

  /** Section keys the user has collapsed in the sidebar (all expanded by default). */
  private readonly collapsedSections = signal<ReadonlySet<string>>(new Set());

  protected isCollapsed(key: string): boolean {
    return this.collapsedSections().has(key);
  }

  protected toggleSection(key: string): void {
    this.collapsedSections.update((current) => {
      const next = new Set(current);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  }

  /**
   * Sections/items the signed-in user may reach, recomputed from their roles.
   * Items they can't access are dropped, and a section with no visible items
   * disappears entirely — so the sidebar only ever shows reachable links.
   */
  protected readonly visibleSections = computed(() =>
    this.sections
      .map((section) => ({
        ...section,
        items: section.items.filter((item) => this.auth.hasAnyRole(item.roles)),
      }))
      .filter((section) => section.items.length > 0),
  );

  protected logout(): void {
    this.auth.logout();
  }
}
