import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';
import { StorefrontFeaturesService, type AlertDto } from 'data-access';
import { Icon } from 'ui';

/** Delay before the toast slides in, so it reads as a notification, not page layout. */
const ENTER_DELAY_MS = 900;

/**
 * Site-wide news-alert notification, styled like a desktop (Windows) toast:
 * a card pinned to the bottom-end corner of the viewport, fed by
 * GET /api/home/alerts (published, non-expired `alert`-category news).
 *
 * Shows one alert at a time; when several are live the footer grows a
 * prev/next pager with a "1 / 3" counter. Closing (X) or opening ("Read
 * more") hides that alert and advances to the next — but the dismissal is
 * only in memory, and every navigation to the home page re-arms the toast,
 * so visitors see the live alerts again on each home visit.
 *
 * SSR-safe: nothing renders on the server or during hydration — the card
 * enters only after `afterNextRender` plus a short delay, which is also what
 * makes it feel like an incoming notification. Positioning uses logical
 * properties so the card sits bottom-left in RTL.
 */
@Component({
  selector: 'app-news-alert-toast',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './news-alert-toast.html',
  styleUrl: './news-alert-toast.scss',
})
export class NewsAlertToast {
  private readonly features = inject(StorefrontFeaturesService);
  private readonly router = inject(Router);

  private readonly alerts = this.features.alertsResource();
  /** False on the server and during hydration; flips after the enter delay. */
  private readonly ready = signal(false);
  /** Alert ids closed this session — cleared again on every home-page visit. */
  private readonly dismissed = signal<ReadonlySet<number>>(new Set());
  /** Requested position in the stack; `safeIndex` clamps it as alerts drop out. */
  private readonly index = signal(0);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  constructor() {
    afterNextRender(() => {
      setTimeout(() => this.ready.set(true), ENTER_DELAY_MS);
    });

    // Re-arm on every home visit: alerts closed earlier in the session come
    // back (with the slide-in animation) each time the visitor lands on home.
    effect(() => {
      if (this.isHome(this.url())) {
        this.dismissed.set(new Set());
        this.index.set(0);
      }
    });
  }

  /** Live alerts minus the ones closed since the last home visit. */
  protected readonly visible = computed<readonly AlertDto[]>(() => {
    if (!this.ready()) {
      return [];
    }
    const dismissed = this.dismissed();
    return (this.alerts.value() ?? []).filter((a) => !dismissed.has(a.id));
  });

  protected readonly count = computed(() => this.visible().length);

  protected readonly safeIndex = computed(() =>
    Math.min(this.index(), Math.max(0, this.count() - 1)),
  );

  protected readonly current = computed<AlertDto | null>(
    () => this.visible()[this.safeIndex()] ?? null,
  );

  protected prev(): void {
    this.index.set(Math.max(0, this.safeIndex() - 1));
  }

  protected next(): void {
    this.index.set(Math.min(this.count() - 1, this.safeIndex() + 1));
  }

  /** Hide for now; the stack advances or the card disappears. */
  protected close(alert: AlertDto): void {
    this.dismissed.update((s) => new Set(s).add(alert.id));
  }

  /** Opening an alert counts as having seen it — hide alongside navigation. */
  protected view(alert: AlertDto): void {
    this.close(alert);
  }

  /** A CTA link that points off-site (rendered as a plain anchor, not a route). */
  protected isExternal(url: string | null): boolean {
    return !!url && /^https?:\/\//i.test(url);
  }

  /** Where "Read more" goes when the CTA isn't an external URL. */
  protected routeTarget(alert: AlertDto): string | unknown[] {
    const url = alert.alertCtaUrl;
    return url && !this.isExternal(url) ? url : ['/news', alert.slug ?? ''];
  }

  /** True for the home route, ignoring query string and fragment. */
  private isHome(url: string): boolean {
    return url.split(/[?#]/)[0] === '/';
  }
}
