import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DomSanitizer, type SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import { StorefrontFeaturesService, type NewsDetailDto } from 'data-access';
import { Button, Tile } from 'ui';
import { SeoService } from '../../core/seo.service';

/** Maps a category slug to its badge label key (null for uncategorised / unknown). */
const BADGE_KEYS: Readonly<Record<string, string>> = {
  'success-story': 'news.badge.success_story',
  activity: 'news.badge.activity',
  alert: 'news.badge.alert',
};

/**
 * True when the body is a complete HTML document (`<!DOCTYPE …>` / `<html>`) rather
 * than a fragment — those render in a sandboxed iframe so their own `<head>`/`<style>`
 * design applies without leaking into the page.
 */
function isFullHtmlDocument(html: string): boolean {
  const head = html.trimStart().slice(0, 15).toLowerCase();
  return head.startsWith('<!doctype') || head.startsWith('<html');
}

/** News article (`/news/:slug`): server-rendered; body is trusted admin-authored HTML. */
@Component({
  selector: 'app-news-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, RouterLink, Button, Tile, MoneyPipe],
  templateUrl: './news-detail.html',
  styleUrl: './news-detail.scss',
})
export class NewsDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(StorefrontFeaturesService);
  private readonly seo = inject(SeoService);
  private readonly language = inject(LanguageService);
  private readonly sanitizer = inject(DomSanitizer);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly item = signal<NewsDetailDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));

  /**
   * The article body rendered as-is: admin-authored HTML is trusted (only staff can
   * write it), and bypassing sanitization keeps inline styles / rich markup that the
   * default `[innerHTML]` sanitizer would otherwise strip.
   */
  protected readonly bodyHtml = computed<SafeHtml | null>(() => {
    const html = this.item()?.fullContent;
    return html ? this.sanitizer.bypassSecurityTrustHtml(html) : null;
  });

  /** Whether the body is a full HTML document (iframe) or a fragment (inline). */
  protected readonly isFullDocument = computed(() =>
    isFullHtmlDocument(this.item()?.fullContent ?? ''),
  );

  /**
   * Grow the sandboxed article frame to fit its document, so the page scrolls as one.
   * Runs on the iframe `load` event (after its subresources), client-side only.
   */
  protected resizeFrame(event: Event): void {
    const frame = event.target as HTMLIFrameElement;
    const doc = frame.contentDocument;
    if (doc?.documentElement) {
      frame.style.height = `${doc.documentElement.scrollHeight + 24}px`;
    }
  }

  /** The i18n key for the category badge, or null when there's nothing to show. */
  protected badgeKey(slug: string | null): string | null {
    return slug ? (BADGE_KEYS[slug] ?? null) : null;
  }

  constructor() {
    effect(() => {
      const slug = this.params().get('slug');
      if (!slug) {
        return;
      }
      this.loading.set(true);
      this.service.newsDetail(slug).subscribe({
        next: (item) => {
          this.item.set(item);
          this.loading.set(false);
          this.seo.update({
            title: item.metaTitle || item.name || slug,
            description: item.metaDescription ?? item.shortContent ?? undefined,
            image: item.thumbnailUrl ?? undefined,
            type: 'article',
          });
        },
        error: () => {
          this.item.set(null);
          this.loading.set(false);
        },
      });
    });
  }
}
