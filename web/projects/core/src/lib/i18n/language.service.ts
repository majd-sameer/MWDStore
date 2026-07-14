import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import {
  computed,
  effect,
  inject,
  Injectable,
  PLATFORM_ID,
  REQUEST,
  signal,
} from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { LocaleState } from 'data-access';

export type Lang = 'en' | 'ar';

const SUPPORTED: readonly Lang[] = ['en', 'ar'];
const COOKIE = 'atb_lang';
const ONE_YEAR = 60 * 60 * 24 * 365;

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly request = inject(REQUEST, { optional: true });
  private readonly translate = inject(TranslateService, { optional: true });

  private readonly localeState = inject(LocaleState);

  private readonly _lang = signal<Lang>(this.detectInitial());
  readonly lang = this._lang.asReadonly();
  readonly dir = computed<'ltr' | 'rtl'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));
  readonly isRtl = computed(() => this.dir() === 'rtl');

  constructor() {
    // Single source of truth for applying the language: runs on init (server +
    // browser) and on every subsequent toggle. SSR flushes effects before
    // serialization, so the server HTML already carries the right lang/dir.
    effect(() => {
      const lang = this._lang();
      const dir = this.dir();
      const root = this.document.documentElement;
      root.setAttribute('lang', lang);
      root.setAttribute('dir', dir);
      this.translate?.use(lang);
      // Drive data-access's locale signal so storefront content httpResources
      // re-fetch server-localized data the moment the language changes.
      this.localeState.setLanguage(lang);
      if (this.isBrowser) {
        this.document.cookie = `${COOKIE}=${lang}; path=/; max-age=${ONE_YEAR}; samesite=lax`;
      }
    });
  }

  use(lang: Lang): void {
    if (SUPPORTED.includes(lang) && lang !== this._lang()) {
      this._lang.set(lang);
    }
  }

  toggle(): void {
    this._lang.set(this._lang() === 'ar' ? 'en' : 'ar');
  }

  private detectInitial(): Lang {
    const cookie = this.readCookie();
    if (cookie && SUPPORTED.includes(cookie as Lang)) {
      return cookie as Lang;
    }
    if (this.isBrowser) {
      const nav = this.document.defaultView?.navigator?.language ?? '';
      if (nav.toLowerCase().startsWith('ar')) {
        return 'ar';
      }
    }
    return 'en';
  }

  private readCookie(): string | null {
    const source = this.isBrowser
      ? this.document.cookie
      : (this.request?.headers?.get('cookie') ?? '');
    const match = source.match(new RegExp(`(?:^|;\\s*)${COOKIE}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  }
}
