import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { AlertDto } from 'data-access';
import { Icon } from 'ui';

const DISMISS_PREFIX = 'dismissed_alert_';

/**
 * Slim announcement band at the very top of the home page, fed by
 * GET /api/home/alerts (published, non-expired `alert`-category news). Design uses
 * tokens only so it sits inside the visual identity: gold `--accent-soft` band, a
 * gold pill badge, navy/ink text. Stacks up to three alerts; each can be dismissed
 * (persisted in `localStorage`) and renders nothing when there are none.
 *
 * SSR-safe: the server and the first client render show every alert (so hydration
 * matches); the per-visitor dismiss filter is applied only after hydration, so a
 * previously dismissed alert simply drops out on the client with no mismatch.
 */
@Component({
  selector: 'app-alert-band',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './alert-band.html',
  styleUrl: './alert-band.scss',
})
export class AlertBand {
  readonly items = input<readonly AlertDto[]>([]);

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  /** False on the server + during hydration; true once we may read localStorage. */
  private readonly filterReady = signal(false);
  /** Bumped on dismiss so `visible` recomputes. */
  private readonly dismissedTick = signal(0);

  constructor() {
    afterNextRender(() => this.filterReady.set(true));
  }

  /** Up to three alerts, minus any the visitor has dismissed (once hydrated). */
  protected readonly visible = computed<readonly AlertDto[]>(() => {
    this.dismissedTick();
    const all = this.items().slice(0, 3);
    if (!this.isBrowser || !this.filterReady()) {
      return all;
    }
    return all.filter((a) => localStorage.getItem(DISMISS_PREFIX + a.id) !== '1');
  });

  protected dismiss(alert: AlertDto): void {
    if (!this.isBrowser) {
      return;
    }
    localStorage.setItem(DISMISS_PREFIX + alert.id, '1');
    this.dismissedTick.update((n) => n + 1);
  }

  /** A CTA link that points off-site (rendered as a plain anchor, not a route). */
  protected isExternal(url: string | null): boolean {
    return !!url && /^https?:\/\//i.test(url);
  }

  /** Where the alert text links when it isn't an external CTA. */
  protected routeTarget(alert: AlertDto): string | unknown[] {
    const url = alert.alertCtaUrl;
    return url && !this.isExternal(url) ? url : ['/news', alert.slug ?? ''];
  }
}
