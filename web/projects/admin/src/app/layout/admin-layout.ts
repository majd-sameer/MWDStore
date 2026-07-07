import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import {
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService, LanguageService } from 'core';

interface NavItem {
  readonly path: string;
  /** Translation key under `nav.` (also used as the track id). */
  readonly key: string;
  /** Bootstrap Icons class, e.g. `bi-box-seam`. */
  readonly icon: string;
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
 * grouped into sections (Catalog / Sales / People / Content / System) plus a
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

  protected readonly sections: readonly NavSection[] = [
    {
      key: null,
      items: [{ path: '/', key: 'dashboard', icon: 'bi-grid-1x2', exact: true }],
    },
    {
      key: 'catalog',
      items: [
        { path: '/products', key: 'products', icon: 'bi-box-seam' },
        { path: '/categories', key: 'categories', icon: 'bi-folder2' },
        { path: '/brands', key: 'brands', icon: 'bi-tag' },
        { path: '/product-options', key: 'options', icon: 'bi-sliders' },
        { path: '/product-attributes', key: 'attributes', icon: 'bi-list-check' },
        { path: '/product-templates', key: 'templates', icon: 'bi-clipboard' },
        { path: '/inventory', key: 'inventory', icon: 'bi-clipboard-data' },
        { path: '/warehouses', key: 'warehouses', icon: 'bi-building' },
      ],
    },
    {
      key: 'sales',
      items: [
        { path: '/orders', key: 'orders', icon: 'bi-receipt' },
        { path: '/promotions', key: 'promotions', icon: 'bi-ticket-perforated' },
        { path: '/shipping', key: 'shipping', icon: 'bi-truck' },
        { path: '/payments', key: 'payments', icon: 'bi-credit-card' },
        { path: '/taxes', key: 'taxes', icon: 'bi-percent' },
      ],
    },
    {
      key: 'people',
      items: [
        { path: '/customers', key: 'customers', icon: 'bi-people' },
        { path: '/users', key: 'users', icon: 'bi-person-badge' },
        { path: '/vendors', key: 'vendors', icon: 'bi-shop' },
        { path: '/moderation', key: 'moderation', icon: 'bi-shield-check' }
        /*,
        { path: '/contacts', key: 'contacts', icon: 'bi-envelope' },*/
      ],
    },
    {
      key: 'content',
      items: [
      /*  { path: '/pages', key: 'pages', icon: 'bi-file-earmark-text' },
        { path: '/menus', key: 'menus', icon: 'bi-list-ul' },*/
        { path: '/news', key: 'news', icon: 'bi-newspaper' },
      ],
    },
    {
      key: 'system',
      items: [
        { path: '/locations', key: 'countries', icon: 'bi-globe2' },
       /* { path: '/localization', key: 'localization', icon: 'bi-translate' },*/
        { path: '/settings', key: 'settings', icon: 'bi-gear' },
      /*  { path: '/logs', key: 'logs', icon: 'bi-journal-text' },*/
      ],
    },
  ];

  protected logout(): void {
    this.auth.logout();
  }
}
