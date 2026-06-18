import { from, type Observable } from 'rxjs';
import type { TranslateLoader, TranslationObject } from '@ngx-translate/core';

/**
 * Loads i18n dictionaries by importing the bundled JSON modules directly,
 * rather than fetching them over HTTP.
 *
 * Why not the HTTP loader: the storefront's `baseUrlInterceptor` rewrites every
 * root-relative `/...` request to the API origin during SSR, which would send a
 * request for `/assets/i18n/en.json` to Store.Api. Importing the JSON keeps
 * translation loading self-contained and identical on server and browser, so
 * the first server paint already has its copy with no flash.
 *
 * Unknown languages fall back to English.
 */
export class JsonTranslateLoader implements TranslateLoader {
  getTranslation(lang: string): Observable<TranslationObject> {
    return from(loadDictionary(lang));
  }
}

async function loadDictionary(lang: string): Promise<TranslationObject> {
  switch (lang) {
    case 'ar': {
      const mod = await import('../../assets/i18n/ar.json');
      return mod.default as TranslationObject;
    }
    default: {
      const mod = await import('../../assets/i18n/en.json');
      return mod.default as TranslationObject;
    }
  }
}
