import { Injectable, signal } from '@angular/core';

/**
 * Holds the active request language for storefront content reads.
 *
 * `core`'s `LanguageService` pushes the active language here whenever it changes, and the
 * storefront catalog / content `httpResource` factories read {@link language} (and send it as a
 * `culture` query param) so they re-fetch server-localized content the moment the user switches
 * language — no page refresh. The actual `Accept-Language` header is still set by `core`'s
 * interceptor; this only makes the reactive request depend on the language.
 *
 * Lives in `data-access` (not `core`) because `core` depends on `data-access`, not the reverse.
 */
@Injectable({ providedIn: 'root' })
export class LocaleState {
  private readonly _language = signal<string>('en');

  /** Active language code (e.g. `'en'` / `'ar'`). */
  readonly language = this._language.asReadonly();

  setLanguage(language: string): void {
    if (language !== this._language()) {
      this._language.set(language);
    }
  }
}
