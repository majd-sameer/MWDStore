import { from, type Observable } from 'rxjs';
import type { TranslateLoader, TranslationObject } from '@ngx-translate/core';


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
