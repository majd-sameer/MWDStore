import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageService } from '../i18n/language.service';

/**
 * Sends `Accept-Language: <active lang>` on every API request so Store.Api can
 * return product / category content in the active language. The header reflects
 * the {@link LanguageService} signal, so it is correct on the server (from the
 * cookie) and in the browser, and follows runtime language switches.
 *
 * Until the API serves localized content it returns English, which is the
 * intended English fallback (§6.5).
 */
export const acceptLanguageInterceptor: HttpInterceptorFn = (req, next) => {
  const lang = inject(LanguageService).lang();
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
