import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';

/**
 * CTA / newsletter band per supported-doc/HOME-PAGE.md §9: ivory rounded card,
 * copy on the left and a white pill subscribe form (email input + green
 * button) with a privacy note on the right. There is no newsletter endpoint
 * yet, so submit just acknowledges with a toast and clears the field.
 * Collapses to one column under 980px.
 */
@Component({
  selector: 'app-cta-band',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  templateUrl: './cta-band.html',
  styleUrl: './cta-band.scss',
})
export class CtaBand {
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly email = signal('');

  protected subscribe(event: Event): void {
    event.preventDefault();
    if (!this.email().trim()) {
      return;
    }
    this.toast.success(this.translate.instant('home.cta_thanks'));
    this.email.set('');
  }
}
