import { inject, Pipe, PipeTransform } from '@angular/core';

import { LanguageService } from './language.service';

/** ISO 4217 code for the currency every amount in the app is stored/priced in. */
const CURRENCY = 'JOD';

/**
 * Renders Jordanian dinars with `Intl.NumberFormat`, keyed off the active UI
 * language: English formats under the `en` locale ("JOD 2,838.000"), Arabic
 * under `ar-JO-u-nu-latn` — Jordanian Arabic with the `-u-nu-latn` Unicode
 * extension forcing Western (Latin) digits instead of Arabic-Indic, and the
 * locale's own Arabic currency symbol ("د.أ"). `Intl` derives JOD's 3-decimal
 * minor unit automatically, so no manual digits config is needed.
 *
 * Drop-in replacement for `| currency` on dinar amounts — `{{ total | money }}`.
 *
 * Impure (like ngx-translate's pipe) so the formatting re-renders on a
 * language toggle: the bound amount doesn't change, only the active `lang`
 * signal. `Intl.NumberFormat` runs identically on server and browser, so this
 * stays SSR-safe without touching `window`.
 */
@Pipe({ name: 'money', pure: false })
export class MoneyPipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: number | string | null | undefined): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }
    const amount = typeof value === 'string' ? Number(value) : value;
    if (Number.isNaN(amount)) {
      return '';
    }
    const locale = this.language.lang() === 'ar' ? 'ar-JO-u-nu-latn' : 'en';
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency: CURRENCY,
    }).format(amount);
  }
}
