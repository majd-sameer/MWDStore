import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SeoService } from '../../core/seo.service';
import { FaqContentStore } from '../../core/faq-content.store';

/**
 * FAQ / الأسئلة الشائعة (`/pages/faq`). Editorial page whose copy is CMS-editable: the heading and
 * each question/answer read their `faq` content block and fall back to the hard-coded `faq.*`
 * translation when a block is absent — the same pattern the About page uses. Answers render inside
 * native `<details>` disclosure elements so the page works without JavaScript and mirrors in RTL.
 */
@Component({
  selector: 'app-faq',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  templateUrl: './faq.html',
  styleUrl: './faq.scss',
})
export class Faq {
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);
  protected readonly content = inject(FaqContentStore);

  /** Built-in Q&A used only when the API returns no blocks (offline/first paint). */
  protected readonly fallbackItems = [1, 2, 3, 4, 5, 6] as const;

  private readonly metaTitle = toSignal(this.translate.stream('faq.meta_title'));
  private readonly metaDescription = toSignal(
    this.translate.stream('faq.meta_description'),
  );

  constructor() {
    effect(() => {
      const title = this.metaTitle();
      if (title) {
        this.seo.update({ title, description: this.metaDescription() });
      }
    });
  }
}
