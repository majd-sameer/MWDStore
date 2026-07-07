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
  templateUrl: './language-switcher.html',
  styleUrl: './language-switcher.scss',
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);
}
