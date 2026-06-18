import {
  ApplicationConfig,
  DEFAULT_CURRENCY_CODE,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideTranslateService, TranslateLoader } from '@ngx-translate/core';
import { AuthService, LanguageService, provideCore } from 'core';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { catchError, firstValueFrom, of } from 'rxjs';

import { routes } from './app.routes';
import { environment } from '../environments/environment';
import { JsonTranslateLoader } from './core/translate-loader';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideCore({ apiBaseUrl: environment.apiBaseUrl }),
    // Jordanian dinar is the store currency. Prices render through core's
    // language-aware `| money` pipe (Arabic "أ.د", otherwise "JOD"); this
    // default just backstops any stray `| currency` with JOD's 3 decimals.
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'JOD' },
    // Chart.js registerables for the dashboard analytics (ng2-charts).
    provideCharts(withDefaultRegisterables()),
    // i18n: English/Arabic with RTL flipping via the shared core
    // LanguageService. The custom loader imports bundled JSON (no HTTP).
    provideTranslateService({
      lang: 'en',
      fallbackLang: 'en',
      // Explicit ClassProvider (not the bare class) so the loader is always
      // instantiated with `new` (see the storefront's identical wiring).
      loader: { provide: TranslateLoader, useClass: JsonTranslateLoader },
    }),
    // Instantiate LanguageService on boot so the persisted cookie is applied
    // (sets <html lang/dir> and the active translate language) before render.
    provideAppInitializer(() => {
      inject(LanguageService);
    }),
    // The access token lives only in memory, so a full page reload starts
    // unauthenticated. Attempt one silent refresh (httpOnly cookie) before the
    // router resolves, so a signed-in admin who reloads keeps their session
    // instead of being bounced to /login. Failures are swallowed — they just
    // mean "not signed in".
    provideAppInitializer(() => {
      const auth = inject(AuthService);
      return firstValueFrom(auth.refresh().pipe(catchError(() => of(''))));
    }),
  ],
};
