import { Pipe, type PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

/**
 * Translates a backend category by its slug via the `categories.<slug>` key,
 * falling back to the supplied display name (then the slug) when no translation
 * exists — e.g. a category the admin added without an i18n entry yet. Used
 * everywhere a category from the API is shown: the header/footer nav, the home
 * cattiles, the shop filters and the product-card eyebrow.
 *
 * Impure so it re-resolves on a language switch (the co-rendered TranslatePipe
 * marks the host for check, re-running this pipe with the new language).
 *
 * @example {{ cat.slug | categoryLabel: cat.name }}
 */
@Pipe({ name: 'categoryLabel', pure: false })
export class CategoryLabelPipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(slug: string | null | undefined, fallback?: string | null): string {
    if (!slug) {
      return fallback ?? '';
    }
    const key = `categories.${slug}`;
    const label = this.translate.instant(key);
    return label === key ? (fallback ?? slug) : label;
  }
}
