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
  styles: [
    `
      :host {
        display: block;
        min-height: 100vh;
      }
      .admin-sidebar {
        position: fixed;
        inset-block: 0;
        inset-inline-start: 0;
        inline-size: 248px;
        background: linear-gradient(180deg, var(--navy), var(--navy-deep));
        display: flex;
        flex-direction: column;
      }
      .admin-main {
        margin-inline-start: 248px;
      }
      .brand-logo {
        block-size: 36px;
        inline-size: auto;
      }
      .brand-name {
        font-size: 0.92rem;
        font-weight: 600;
        line-height: 1.2;
      }
      .brand-sub {
        font-size: 0.72rem;
        color: var(--accent-soft);
      }
      .admin-nav {
        flex: 1 1 auto;
        overflow-y: auto;
        scrollbar-width: thin;
        scrollbar-color: rgba(255, 255, 255, 0.25) transparent;
        margin-inline: -0.5rem;
        padding-inline: 0.5rem;
      }
      .nav-section-label {
        font-size: 0.66rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: rgba(255, 255, 255, 0.42);
        padding: 1rem 0.75rem 0.35rem;
      }
      .admin-nav .nav-link {
        display: flex;
        align-items: center;
        color: rgba(255, 255, 255, 0.78);
        border-radius: 0.5rem;
        padding: 0.42rem 0.75rem;
        font-size: 0.92rem;
      }
      .admin-nav .nav-link .bi {
        inline-size: 1.5rem;
        flex-shrink: 0;
        text-align: center;
        margin-inline-end: 0.45rem;
        font-size: 1rem;
        opacity: 0.85;
      }
      .admin-nav .nav-link:hover {
        color: #fff;
        background: rgba(255, 255, 255, 0.08);
      }
      .admin-nav .nav-link.active {
        color: #fff;
        background: var(--green);
        box-shadow: var(--sh-green);
      }
      .admin-nav .nav-link.active .bi {
        opacity: 1;
      }
      .topbar {
        background: color-mix(in srgb, var(--surface) 88%, transparent);
        backdrop-filter: blur(8px);
        border-block-end: 1px solid var(--line-2);
      }
      .lang-switch {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        border: 1px solid var(--line-strong);
        border-radius: 999px;
        background: var(--surface);
        color: var(--ink);
        padding: 0.3rem 0.85rem;
        font-weight: 600;
        font-size: 0.85rem;
        cursor: pointer;
      }
      .lang-switch:hover {
        background: var(--surface-2);
      }
      .user-chip {
        display: inline-flex;
        align-items: center;
        gap: 0.6rem;
      }
      .user-avatar {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        inline-size: 34px;
        block-size: 34px;
        border-radius: 50%;
        background: var(--green-soft);
        color: var(--green-strong);
        font-size: 0.85rem;
        font-weight: 700;
      }
      @media (max-width: 991.98px) {
        .admin-sidebar {
          inline-size: 64px;
        }
        .admin-main {
          margin-inline-start: 64px;
        }
        .admin-nav .nav-link .label,
        .nav-section-label,
        .brand-text,
        .user-chip .user-name {
          display: none;
        }
        .admin-nav .nav-link {
          justify-content: center;
        }
        .admin-nav .nav-link .bi {
          margin-inline-end: 0;
        }
      }
    `,
  ],
  template: `
    <aside class="admin-sidebar d-flex flex-column p-3">
      <a
        routerLink="/"
        class="navbar-brand text-white d-flex align-items-center gap-2 mb-2 text-decoration-none"
      >
        <img class="brand-logo" src="logo-gold.png" alt="" />
        <span class="brand-text d-flex flex-column">
          <span class="brand-name">{{ 'brand.name' | translate }}</span>
          <span class="brand-sub">{{ 'brand.console' | translate }}</span>
        </span>
      </a>

      <nav class="admin-nav nav nav-pills flex-column flex-nowrap">
        @for (section of sections; track section.key) {
          @if (section.key) {
            <div class="nav-section-label">{{ 'nav.' + section.key | translate }}</div>
          }
          @for (item of section.items; track item.key) {
            <a
              class="nav-link"
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: item.exact ?? false }"
              [title]="'nav.' + item.key | translate"
            >
              <i class="bi {{ item.icon }}" aria-hidden="true"></i>
              <span class="label">{{ 'nav.' + item.key | translate }}</span>
            </a>
          }
        }
      </nav>
    </aside>

    <div class="admin-main d-flex flex-column min-vh-100">
      <header class="topbar navbar px-4 py-2 sticky-top">
        <div class="container-fluid justify-content-end gap-3">
          <button
            type="button"
            class="lang-switch"
            [attr.aria-label]="'common.language' | translate"
            (click)="language.toggle()"
          >
            <i class="bi bi-translate" aria-hidden="true"></i>
            {{ 'common.language' | translate }}
          </button>
          <span class="user-chip">
            <span class="user-avatar">{{ initials() }}</span>
            <span class="user-name text-body-secondary small">
              {{ auth.fullName() || auth.email() }}
            </span>
          </span>
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm"
            (click)="logout()"
          >
            {{ 'topbar.signout' | translate }}
          </button>
        </div>
      </header>

      <main class="container-fluid px-4 py-4 flex-grow-1">
        <router-outlet />
      </main>
    </div>
  `,
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
