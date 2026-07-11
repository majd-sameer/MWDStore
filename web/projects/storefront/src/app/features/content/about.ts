import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { DomSanitizer, type SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';
import { SeoService } from '../../core/seo.service';
import { AboutContentStore } from '../../core/about-content.store';

/**
 * About / من نحن (`/pages/about-us`) — per supported-doc/ABOUT-PAGE.md.
 * Editorial page whose copy is CMS-editable: each line reads its content block
 * (`about` page) and falls back to the hard-coded `about.*` translation when a
 * block is absent, so the page never renders empty if the API/seeder lags —
 * the same pattern the home sections use. Three stacked sections: full-bleed
 * navy hero, "how we work" numbered steps, and the five value cards. The layout
 * is built with logical properties so the RTL design mirrors to LTR
 * automatically.
 */
@Component({
  selector: 'app-about',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './about.html',
  styleUrl: './about.scss',
})
export class About {
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly content = inject(AboutContentStore);

  /**
   * The story video's embeddable URL, derived from the admin-set YouTube link
   * (`about-video`/`youtube`). Any watch / share / embed form is normalized to a
   * privacy-friendly `youtube-nocookie` embed (preserving a `t=` start time), then
   * marked safe for the iframe. `null` when unset or unparseable, so the template
   * hides the whole section instead of rendering an empty frame.
   */
  protected readonly videoEmbed = computed<SafeResourceUrl | null>(() => {
    const raw = this.content.block('about-video', 'youtube')?.linkUrl;
    const embed = toYouTubeEmbed(raw);
    return embed ? this.sanitizer.bypassSecurityTrustResourceUrl(embed) : null;
  });

  protected readonly steps = [1, 2, 3, 4] as const;
  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
    { icon: 'spark', key: 'quality' },
  ];

  // `stream` re-emits on language switch, so the SEO tags follow the active
  // language (instant() would freeze the first language's strings).
  private readonly metaTitle = toSignal(this.translate.stream('about.meta_title'));
  private readonly metaDescription = toSignal(
    this.translate.stream('about.meta_description'),
  );

  constructor() {
    effect(() => {
      const title = this.metaTitle();
      if (title) {
        this.seo.update({ title, description: this.metaDescription() });
      }
    });
  }
}

/** Parse a YouTube `t=` / `start=` value ("31", "31s", "1m30s", "1h2m3s") into seconds. */
function parseStart(raw: string | null): number {
  if (!raw) {
    return 0;
  }
  if (/^\d+$/.test(raw)) {
    return Number(raw);
  }
  const match = raw.match(/(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?/);
  if (!match) {
    return 0;
  }
  const [, h, m, s] = match;
  return Number(h ?? 0) * 3600 + Number(m ?? 0) * 60 + Number(s ?? 0);
}

/**
 * Normalize any admin-entered YouTube link (watch / share / embed) into a
 * `youtube-nocookie.com/embed/{id}` URL, preserving a start time. Returns `null`
 * for empty or non-YouTube input so callers can hide the player.
 */
export function toYouTubeEmbed(raw: string | null | undefined): string | null {
  if (!raw?.trim()) {
    return null;
  }
  let url: URL;
  try {
    url = new URL(raw.trim());
  } catch {
    return null;
  }
  const host = url.hostname.replace(/^www\./, '');
  let id = '';
  if (host === 'youtu.be') {
    id = url.pathname.slice(1);
  } else if (host === 'youtube.com' || host === 'youtube-nocookie.com') {
    id = url.pathname.startsWith('/embed/')
      ? url.pathname.slice('/embed/'.length)
      : (url.searchParams.get('v') ?? '');
  }
  if (!/^[\w-]{11}$/.test(id)) {
    return null;
  }
  const start = parseStart(url.searchParams.get('t') ?? url.searchParams.get('start'));
  const query = start > 0 ? `?start=${start}` : '';
  return `https://www.youtube-nocookie.com/embed/${id}${query}`;
}
