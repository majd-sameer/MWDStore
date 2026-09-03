import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';
import { Icon } from 'ui';
import { CartStore } from '../core/cart.store';
import { CartDrawerService } from '../core/cart-drawer.service';
import { LanguageSwitcher } from './language-switcher';

interface MainLink {
  readonly key: string;
  readonly link: string;
  readonly queryParams?: Record<string, string>;
}

/**
 * Storefront chrome: announce bar, wordmark, a hardcoded primary nav
 * (Home / Store Sections / Our News / About Us) and the language / account /
 * tracking / cart actions (with a live cart-count badge). Below the lg
 * breakpoint the header shows the logo only: navigation, cart, account and
 * language all live in the app-style bottom tab bar (layout/bottom-nav).
 * All copy is keyed through ngx-translate.
 */
@Component({
  selector: 'app-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TranslatePipe, Icon, LanguageSwitcher],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartStore);
  protected readonly cartDrawer = inject(CartDrawerService);

  /** True for a moment after the bag count changes, to bump the badge. */
  protected readonly bump = signal(false);
  private bumpTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    let previous = untracked(() => this.cart.count());
    effect(() => {
      const count = this.cart.count();
      if (count === previous) {
        return;
      }
      previous = count;
      if (count === 0) {
        return;
      }
      untracked(() => {
        clearTimeout(this.bumpTimer);
        this.bump.set(true);
        this.bumpTimer = setTimeout(() => this.bump.set(false), 500);
      });
    });
  }

  /**
   * Hardcoded primary nav. "Store Sections" opens the category hub (/categories)
   * so shoppers pick a section before seeing products.
   */
  protected readonly mainLinks: readonly MainLink[] = [
    { key: 'home', link: '/' },
    { key: 'sections', link: '/categories' },
    { key: 'news', link: '/news' },
    { key: 'about_us', link: '/pages/about-us' },
  ];
}
