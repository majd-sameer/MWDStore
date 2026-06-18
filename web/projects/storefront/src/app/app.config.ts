import {
  ApplicationConfig,
  DEFAULT_CURRENCY_CODE,
  inject,
  PLATFORM_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { isPlatformBrowser, registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';
import { AuthService, LanguageService, provideCore } from 'core';
import { provideTranslateService, TranslateLoader } from '@ngx-translate/core';

import { routes } from './app.routes';
import { environment } from '../environments/environment';
import { JsonTranslateLoader } from './core/translate-loader';

// Arabic locale data for `| date` (Arabic month names under the active locale).
// Prices stay in Western digits (en-US default) to match the design's tabular
// numerals, so the currency/number pipes need no per-language locale.
registerLocaleData(localeAr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(),
    // Jordanian dinar is the store currency. Prices render through core's
    // language-aware `| money` pipe (Arabic "أ.د", otherwise "JOD"); this
    // default just backstops any stray `| currency` with JOD's 3 decimals.
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'JOD' },
    provideCore({
      apiBaseUrl: environment.apiBaseUrl,
      ssrApiBaseUrl: environment.ssrApiBaseUrl,
    }),
    // i18n: English now; the Arabic dictionary + RTL language switcher arrive in
    // the language step (§6). The custom loader imports bundled JSON so SSR is
    // flash-free (see JsonTranslateLoader).
    provideTranslateService({
      lang: 'en',
      fallbackLang: 'en',
      // Explicit ClassProvider (not the bare class) so the loader is always
      // instantiated with `new` — the bare-class form is mis-wrapped as a
      // factory and fails the prod prerender ("cannot be invoked without new").
      loader: { provide: TranslateLoader, useClass: JsonTranslateLoader },
    }),
    // Instantiate LanguageService before first render so SSR reads the cookie
    // and emits the correct <html lang/dir> with no flash.
    provideAppInitializer(() => {
      inject(LanguageService);
    }),
    // Browser-only silent session restore: a returning user with a valid
    // refresh cookie is signed back in (so their server cart loads and any
    // local guest cart merges); a guest's 401 is swallowed with no redirect.
    // Fire-and-forget so first paint is never blocked on the network.
    provideAppInitializer(() => {
      if (isPlatformBrowser(inject(PLATFORM_ID))) {
        inject(AuthService)
          .restoreSession()
          .subscribe({ error: () => undefined });
      }
    }),
  ],
};
