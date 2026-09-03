import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  PLATFORM_ID,
  computed,
  effect,
  inject,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon, IconName } from 'ui';
import { filter, map } from 'rxjs';
import { CartStore } from '../core/cart.store';

interface Tab {
  readonly key: 'home' | 'store' | 'cart' | 'more';
  readonly icon: IconName;
  readonly link: string;
  /** Route prefixes that light this tab up. */
  readonly match: readonly string[];
}

/**
 * App-style bottom tab bar for phones and tablets (below the lg breakpoint):
 * Home / Store / Cart / More. "More" is a page (features/more) holding the
 * profile, language, order tracking, news and about links, so the small-screen
 * header can be the logo alone.
 *
 * Hidden during checkout and payment so the shopper stays on the funnel.
 * Adds `has-tabbar` to <body>, which reserves space beneath the page and lifts
 * other bottom-anchored UI (toasts, sticky buy bar) above the tabs.
 */
@Component({
  selector: 'app-bottom-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './bottom-nav.html',
  styleUrl: './bottom-nav.scss',
})
export class BottomNav {
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  protected readonly cart = inject(CartStore);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** Path only (no query string / fragment). */
  private readonly path = computed(() => this.url().split(/[?#]/)[0] || '/');

  protected readonly hidden = computed(() => {
    const path = this.path();
    return path.startsWith('/checkout') || path.startsWith('/payment');
  });

  protected readonly tabs: readonly Tab[] = [
    { key: 'home', icon: 'home', link: '/', match: ['/'] },
    { key: 'store', icon: 'store', link: '/categories', match: ['/categories', '/shop', '/products'] },
    { key: 'cart', icon: 'bag', link: '/cart', match: ['/cart'] },
    {
      key: 'more',
      icon: 'menu',
      link: '/more',
      match: ['/more', '/account', '/login', '/register', '/track-order', '/news', '/pages'],
    },
  ];

  protected isActive(tab: Tab): boolean {
    const path = this.path();
    return tab.match.some((prefix) =>
      prefix === '/' ? path === '/' : path === prefix || path.startsWith(prefix + '/'),
    );
  }

  constructor() {
    effect(() => {
      if (!this.isBrowser) {
        return;
      }
      this.document.body.classList.toggle('has-tabbar', !this.hidden());
    });
  }
}
