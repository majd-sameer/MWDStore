import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';
import { CatalogService } from 'data-access';
import { Icon } from 'ui';
import { CartStore } from '../core/cart.store';
import { CartDrawerService } from '../core/cart-drawer.service';
import { CategoryLabelPipe } from '../shared/category-label.pipe';
import { LanguageSwitcher } from './language-switcher';

interface MainLink {
  readonly key: string;
  readonly link: string;
}

interface CategoryLink {
  readonly name: string;
  readonly category: string;
}

/**
 * Storefront chrome: announce bar, wordmark, a hardcoded primary nav
 * (Store / Success story / Categories / About us), a secondary sub-nav of the
 * backend categories, and the search / account / cart icon actions (with a live
 * cart-count badge). On small screens both navs collapse into a logical-side
 * drawer. All copy is keyed through ngx-translate.
 */
@Component({
  selector: 'app-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    Icon,
    LanguageSwitcher,
    CategoryLabelPipe,
  ],
  template: `
    <div class="announce">
      {{ 'announce' | translate }}
    </div>

    <header class="site-header">
      <div class="wrap header-bar">
        <button
          type="button"
          class="icon-btn d-md-none"
          [attr.aria-label]="'nav.menu' | translate"
          [attr.aria-expanded]="menuOpen()"
          (click)="menuOpen.set(true)"
        >
          <lib-icon name="menu" [size]="22" />
        </button>

        <a routerLink="/" class="wordmark" aria-label="MadeWithDetermination">
          <img class="wordmark-logo" src="logo-gold.png" alt="" />
          <span class="wordmark-text">{{ 'brand.name' | translate }}</span>
        </a>

        <nav class="primary-nav d-none d-md-flex" [attr.aria-label]="'nav.menu' | translate">
          @for (link of mainLinks; track link.key) {
            <a
              class="nav-link"
              [routerLink]="link.link"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: true }"
              >{{ 'nav.' + link.key | translate }}</a
            >
          }
        </nav>

        <div class="header-actions">
          <app-language-switcher class="d-none d-sm-inline-flex" />
          <a
            class="icon-btn"
            [routerLink]="auth.isAuthenticated() ? '/account' : '/login'"
            [attr.aria-label]="'nav.account' | translate"
          >
            <lib-icon name="user" [size]="20" />
          </a>
          <a
            class="icon-btn"
            routerLink="/track-order"
            [attr.aria-label]="'nav.track' | translate"
            [attr.title]="'nav.track' | translate"
          >
            <lib-icon name="box" [size]="20" />
          </a>
          <button
            type="button"
            class="icon-btn cart-btn"
            [attr.aria-label]="'nav.cart' | translate"
            (click)="cartDrawer.show()"
          >
            <lib-icon name="bag" [size]="20" />
            @if (cart.count(); as count) {
              <span class="cart-count">{{ count }}</span>
            }
          </button>
        </div>
      </div>
    </header>

    @if (categoryLinks().length) {
      <nav class="sub-nav d-none d-md-block" [attr.aria-label]="'nav.categories' | translate">
        <div class="wrap sub-nav-row">
          @for (link of categoryLinks(); track link.category) {
            <a
              class="sub-link"
              [routerLink]="['/shop']"
              [queryParams]="{ category: link.category }"
              routerLinkActive="active"
              >{{ link.category | categoryLabel: link.name }}</a
            >
          }
        </div>
      </nav>
    }

    @if (menuOpen()) {
      <button
        type="button"
        class="drawer-backdrop"
        [attr.aria-label]="'nav.menu' | translate"
        (click)="menuOpen.set(false)"
      ></button>
      <div class="drawer" role="dialog" [attr.aria-label]="'nav.menu' | translate">
        <div class="drawer-head">
          <span class="wordmark wordmark--ink">
            <img class="wordmark-logo" src="logo-color.png" alt="" />
            <span>{{ 'brand.name' | translate }}</span>
          </span>
          <button type="button" class="icon-btn" aria-label="Close" (click)="menuOpen.set(false)">
            <lib-icon name="x" [size]="22" />
          </button>
        </div>
        <div class="drawer-lang"><app-language-switcher /></div>
        <nav class="drawer-nav" [attr.aria-label]="'nav.menu' | translate">
          @for (link of mainLinks; track link.key) {
            <a
              [routerLink]="link.link"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: link.link === '' }"
              (click)="menuOpen.set(false)"
            >
              {{ 'nav.' + link.key | translate }}
            </a>
          }

          @if (categoryLinks().length) {
            <span class="drawer-section">{{ 'nav.categories' | translate }}</span>
            @for (link of categoryLinks(); track link.category) {
              <a
                class="drawer-sub"
                [routerLink]="['/shop']"
                [queryParams]="{ category: link.category }"
                (click)="menuOpen.set(false)"
                >{{ link.category | categoryLabel: link.name }}</a
              >
            }
          }

          <a
            class="drawer-account"
            [routerLink]="auth.isAuthenticated() ? '/account' : '/login'"
            routerLinkActive="active"
            (click)="menuOpen.set(false)"
          >
            <lib-icon name="user" [size]="18" />
            {{ (auth.isAuthenticated() ? 'nav.account' : 'nav.signin') | translate }}
          </a>
        </nav>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .announce {
      background: var(--titanium);
      color: var(--accent-soft);
      text-align: center;
      font-size: 0.82rem;
      padding-block: 0.5rem;
      padding-inline: 1rem;
    }
    .site-header {
      position: sticky;
      inset-block-start: 0;
      z-index: 1020;
      background: linear-gradient(180deg, var(--navy), var(--navy-deep));
      border-block-end: 1px solid rgba(255, 255, 255, 0.08);
    }
    .header-bar {
      position: relative;
      display: flex;
      align-items: center;
      gap: 1rem;
      block-size: 68px;
    }
    .wordmark {
      display: inline-flex;
      align-items: center;
      gap: 0.6rem;
      font-weight: 700;
      font-size: 1.1rem;
      letter-spacing: -0.01em;
      color: #fff;
      text-decoration: none;
      white-space: nowrap;
    }
    .wordmark--ink {
      color: var(--ink);
    }
    .wordmark-logo {
      block-size: 42px;
      inline-size: auto;
    }
    .primary-nav {
      gap: 1.5rem;
    }
    .primary-nav .nav-link {
      padding: 0;
      color: rgba(255, 255, 255, 0.82);
      font-weight: 500;
      font-size: 0.95rem;
      text-decoration: none;
    }
    .primary-nav .nav-link:hover,
    .primary-nav .nav-link.active {
      color: #fff;
    }
    .sub-nav {
      background: var(--surface);
      border-block-end: 1px solid var(--line);
      position: sticky;
      inset-block-start: 68px;
      z-index: 1019;
    }
    .sub-nav-row {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1.5rem;
      block-size: 46px;
      overflow-x: auto;
      scrollbar-width: none;
    }
    .sub-nav-row::-webkit-scrollbar {
      display: none;
    }
    .sub-nav .sub-link {
      color: var(--ink-2);
      font-weight: 500;
      font-size: 0.9rem;
      text-decoration: none;
      white-space: nowrap;
    }
    .sub-nav .sub-link:hover,
    .sub-nav .sub-link.active {
      color: var(--accent);
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      margin-inline-start: auto;
    }
    .header-actions app-language-switcher {
      color: #fff;
    }
    .site-header .icon-btn {
      color: #fff;
    }
    .site-header .icon-btn:hover {
      background: rgba(255, 255, 255, 0.12);
    }
    .icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 42px;
      block-size: 42px;
      border: 0;
      border-radius: 50%;
      background: transparent;
      color: var(--ink);
      cursor: pointer;
      text-decoration: none;
    }
    .icon-btn:hover {
      background: var(--surface-2);
    }
    .cart-btn {
      position: relative;
    }
    .cart-count {
      position: absolute;
      inset-block-start: 4px;
      inset-inline-end: 2px;
      min-inline-size: 18px;
      block-size: 18px;
      padding-inline: 4px;
      border-radius: 999px;
      background: var(--green);
      color: #fff;
      font-size: 0.68rem;
      font-weight: 700;
      line-height: 18px;
      text-align: center;
    }
    .drawer-backdrop {
      position: fixed;
      inset: 0;
      border: 0;
      padding: 0;
      background: rgba(26, 23, 48, 0.4);
      cursor: pointer;
      z-index: 1040;
    }
    .drawer {
      position: fixed;
      inset-block: 0;
      inset-inline-start: 0;
      inline-size: min(82vw, 320px);
      z-index: 1045;
      background: var(--surface);
      border-inline-end: 1px solid var(--line);
      padding: 1.25rem;
      box-shadow: var(--shadow-lg);
      display: flex;
      flex-direction: column;
    }
    .drawer-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-block-end: 0.75rem;
      padding-block-end: 0.85rem;
      border-block-end: 1px solid var(--line);
      flex: 0 0 auto;
    }
    .drawer-nav {
      display: flex;
      flex-direction: column;
      gap: 2px;
      flex: 1 1 auto;
      min-block-size: 0;
      overflow-y: auto;
      overscroll-behavior: contain;
      -webkit-overflow-scrolling: touch;
    }
    .drawer-nav a {
      position: relative;
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.8rem 0.75rem;
      border-radius: var(--r-sm);
      color: var(--ink);
      font-weight: 600;
      text-decoration: none;
      transition: background-color 0.15s ease, color 0.15s ease;
    }
    .drawer-nav a:hover {
      background: var(--surface-2);
      color: var(--accent);
    }
    .drawer-nav a.active {
      color: var(--accent);
      background: color-mix(in srgb, var(--accent) 12%, transparent);
    }
    .drawer-nav a.active::before {
      content: '';
      position: absolute;
      inset-block: 0.55rem;
      inset-inline-start: 0;
      inline-size: 3px;
      border-radius: 999px;
      background: var(--accent);
    }
    .drawer-section {
      padding: 1rem 0.75rem 0.4rem;
      font-size: 0.72rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: var(--ink-3);
    }
    .drawer-nav a.drawer-sub {
      padding-block: 0.6rem;
      font-weight: 500;
      color: var(--ink-2);
    }
    .drawer-nav a.drawer-account {
      margin-block-start: 0.6rem;
      padding-block-start: 1rem;
      border-block-start: 1px solid var(--line);
      border-radius: 0;
    }
    .drawer-nav a.drawer-account:hover {
      border-radius: var(--r-sm);
    }
    .drawer-lang {
      flex: 0 0 auto;
      padding-block-end: 0.85rem;
      margin-block-end: 0.25rem;
      border-block-end: 1px solid var(--line-2);
    }
    /* Brand text only once there's room for wordmark + centred nav + actions. */
    @media (max-width: 991.98px) {
      .wordmark-text {
        display: none;
      }
    }
    /* From md up the primary nav is in flow and auto-centred between the
       wordmark and the actions, so it can never overlap them. */
    @media (min-width: 768px) {
      .primary-nav {
        margin-inline: auto;
      }
      .header-actions {
        margin-inline-start: 0;
      }
    }
  `,
})
export class Header {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartStore);
  protected readonly cartDrawer = inject(CartDrawerService);
  private readonly catalog = inject(CatalogService);

  protected readonly menuOpen = signal(false);

  private readonly categories = this.catalog.categoriesResource();

  /** Hardcoded primary nav (destinations are placeholders for now). */
  protected readonly mainLinks: readonly MainLink[] = [
    { key: 'home', link: '' },
    { key: 'shop', link: '/shop' },
    { key: 'success_story', link: '/news' },
    { key: 'about_us', link: '/pages/about-us' },
  ];

  /**
   * Sub-nav, derived from the backend categories: top-level (no parent),
   * in-menu, with a usable name + slug, ordered by the admin's display order.
   */
  protected readonly categoryLinks = computed<readonly CategoryLink[]>(() =>
    (this.categories.value() ?? [])
      .filter((c) => c.includeInMenu && c.parentId === null && c.slug && c.name)
      .sort((a, b) => a.displayOrder - b.displayOrder)
      .map((c) => ({ name: c.name as string, category: c.slug as string })),
  );
}
