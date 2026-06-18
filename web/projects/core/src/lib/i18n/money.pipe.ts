import { formatCurrency } from '@angular/common';
import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';

import { LanguageService } from './language.service';

/**
 * Renders Jordanian dinars with a language-aware currency symbol: the Arabic
 * abbreviation "أ.د" when Arabic is the active language, the ISO code "JOD"
 * otherwise. Digits stay Western and JOD keeps its 3 decimals (the amount is
 * formatted under the app's `LOCALE_ID`, which is `en`), matching the design.
 *
 * Drop-in replacement for `| currency` on dinar amounts — `{{ total | money }}`.
 *
 * Impure (like ngx-translate's pipe) so the symbol re-renders on a language
 * toggle: the bound amount doesn't change, only the active `lang` signal.
 */
@Pipe({ name: 'money', pure: false })
export class MoneyPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);
  private readonly language = inject(LanguageService);

  transform(value: number | string | null | undefined, digitsInfo?: string): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }
    const amount = typeof value === 'string' ? Number(value) : value;
    if (Number.isNaN(amount)) {
      return '';
    }
    const symbol = this.language.lang() === 'ar' ? 'أ.د' : 'JOD';
    return formatCurrency(amount, this.locale, symbol, 'JOD', digitsInfo);
  }
}
