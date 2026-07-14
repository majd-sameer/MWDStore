import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter } from 'rxjs';
import { AuthService, LanguageService } from 'core';
import { AREA } from '../core/roles';

interface NavItem {
  readonly path: string;
  readonly key: string;
  readonly icon: string;
  readonly roles: readonly string[];
  readonly exact?: boolean;
}

interface NavSection {
  readonly key: string | null;
  readonly items: readonly NavItem[];
}


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


  protected readonly mobileNavOpen = signal(false);

  constructor() {
    const router = inject(Router);
    router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(() => this.mobileNavOpen.set(false));
  }

  protected toggleMobileNav(): void {
    this.mobileNavOpen.update((open) => !open);
  }

  protected closeMobileNav(): void {
    this.mobileNavOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.mobileNavOpen.set(false);
  }

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
    // Stock management — inventory ∪ fulfillment. Orders appears here too (roles
    // AREA.inventory) so warehouse staff can reach an order to fulfil it; it also
    // lives under Sales, so admins see it in both (like customers).
    {
      key: 'stock',
      items: [
        { path: '/orders', key: 'orders', icon: 'bi-receipt', roles: AREA.inventory },
        { path: '/stock-out', key: 'stockOut', icon: 'bi-box-arrow-up', roles: AREA.inventory },
        { path: '/stock-out-log', key: 'stockOutLog', icon: 'bi-journal-arrow-up', roles: AREA.inventory },
        { path: '/warehouses', key: 'warehouses', icon: 'bi-building', roles: AREA.inventory },
        { path: '/shipping', key: 'shipping', icon: 'bi-truck', roles: AREA.fulfillment },
        { path: '/products', key: 'products', icon: 'bi-box-seam', roles: AREA.catalog },
        { path: '/categories', key: 'categories', icon: 'bi-folder2', roles: AREA.catalog },
        { path: '/brands', key: 'brands', icon: 'bi-tag', roles: AREA.catalog },
        { path: '/product-options', key: 'options', icon: 'bi-sliders', roles: AREA.catalog },
        { path: '/product-attributes', key: 'attributes', icon: 'bi-list-check', roles: AREA.catalog },
        { path: '/product-templates', key: 'templates', icon: 'bi-clipboard', roles: AREA.catalog },
      ],
    },
    // Content management — catalog ∪ content. Phase 5 adds content-blocks here.
    {
      key: 'content',
      items: [
        { path: '/news', key: 'news', icon: 'bi-newspaper', roles: AREA.content },
        { path: '/site-content/home', key: 'siteHome', icon: 'bi-house-door', roles: AREA.content },
        { path: '/site-content/about', key: 'siteAbout', icon: 'bi-info-circle', roles: AREA.content },
        { path: '/site-content/footer', key: 'siteFooter', icon: 'bi-layout-text-window-reverse', roles: AREA.content },
        { path: '/site-content/faq', key: 'siteFaq', icon: 'bi-question-circle', roles: AREA.content },
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
        { path: '/payments', key: 'payments', icon: 'bi-credit-card', roles: AREA.payments },
        { path: '/vendors', key: 'vendors', icon: 'bi-shop', roles: AREA.vendors },
      ],
    },
    // System — user & role management plus settings.
    {
      key: 'system',
      items: [
        { path: '/users', key: 'users', icon: 'bi-person-badge', roles: AREA.users },
        { path: '/locations', key: 'countries', icon: 'bi-globe2', roles: AREA.settings },
        { path: '/audit-log', key: 'auditLog', icon: 'bi-clipboard-check', roles: AREA.settings },
        { path: '/settings', key: 'settings', icon: 'bi-gear', roles: AREA.settings },
      ],
    },
  ];

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
