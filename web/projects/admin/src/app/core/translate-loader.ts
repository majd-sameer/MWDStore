import { from, type Observable } from 'rxjs';
import type { TranslateLoader, TranslationObject } from '@ngx-translate/core';

/**
 * Loads the admin i18n dictionaries by importing the bundled JSON modules
 * directly (same pattern as the storefront): no HTTP fetch, so the base-url
 * interceptor never rewrites the request and the dictionary is available
 * immediately on boot. Unknown languages fall back to English.
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
