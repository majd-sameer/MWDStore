import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from 'core';

/**
 * Header control that flips the whole document between English and Arabic via
 * the shared core LanguageService. Rendered as a two-option segmented control
 * (EN / ع) that highlights the active language. It inherits its colour from the
 * surrounding context (`currentColor`), so it reads correctly both on the navy
 * header and on the light mobile drawer without any per-context overrides.
 */
@Component({
  selector: 'app-language-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div class="lang-seg" role="group" [attr.aria-label]="'common.language_select' | translate">
      <button
        type="button"
        class="seg"
        [class.active]="language.lang() === 'en'"
        [attr.aria-pressed]="language.lang() === 'en'"
        aria-label="English"
        (click)="language.use('en')"
      >
        EN
      </button>
      <button
        type="button"
        class="seg"
        [class.active]="language.lang() === 'ar'"
        [attr.aria-pressed]="language.lang() === 'ar'"
        aria-label="العربية"
        (click)="language.use('ar')"
      >
        ع
      </button>
    </div>
  `,
  styles: `
    :host {
      display: inline-flex;
    }
    .lang-seg {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding: 3px;
      border-radius: 999px;
      border: 1px solid color-mix(in srgb, currentColor 22%, transparent);
      background: color-mix(in srgb, currentColor 8%, transparent);
    }
    .seg {
      min-inline-size: 30px;
      border: 0;
      border-radius: 999px;
      background: transparent;
      color: inherit;
      opacity: 0.65;
      padding-block: 0.2rem;
      padding-inline: 0.55rem;
      font-weight: 700;
      font-size: 0.8rem;
      line-height: 1.1;
      cursor: pointer;
      transition: opacity 0.15s ease, background-color 0.15s ease;
    }
    .seg:hover {
      opacity: 1;
    }
    .seg.active {
      opacity: 1;
      background: color-mix(in srgb, currentColor 16%, transparent);
    }
    .seg:focus-visible {
      outline: 2px solid currentColor;
      outline-offset: 1px;
    }
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);
}
