import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';
import { Icon, IconName } from 'ui';
import { LanguageSwitcher } from '../../layout/language-switcher';

interface MoreLink {
  readonly key: string;
  readonly icon: IconName;
  readonly link: string;
}

/**
 * The "More" tab of the mobile / tablet bottom bar, as a full page: profile,
 * order tracking, news, about, and the language switch. On desktop the same
 * links live in the header, so this page is only ever reached from the tab bar.
 */
@Component({
  selector: 'app-more',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, LanguageSwitcher],
  templateUrl: './more.html',
  styleUrl: './more.scss',
})
export class More {
  protected readonly auth = inject(AuthService);

  protected readonly links = computed<readonly MoreLink[]>(() => {
    const signedIn = this.auth.isAuthenticated();
    return [
      { key: signedIn ? 'account' : 'signin', icon: 'user', link: signedIn ? '/account' : '/login' },
      { key: 'track', icon: 'box', link: signedIn ? '/account/orders' : '/track-order' },
      { key: 'news', icon: 'spark', link: '/news' },
      { key: 'about_us', icon: 'hands', link: '/pages/about-us' },
    ];
  });

  protected onLogout(): void {
    this.auth.logout();
  }
}
