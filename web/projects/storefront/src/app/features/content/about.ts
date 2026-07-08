import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, type IconName } from 'ui';
import { SeoService } from '../../core/seo.service';
import { AboutContentStore } from '../../core/about-content.store';

/**
 * About / من نحن (`/pages/about-us`) — per supported-doc/ABOUT-PAGE.md.
 * Editorial page whose copy is CMS-editable: each line reads its content block
 * (`about` page) and falls back to the hard-coded `about.*` translation when a
 * block is absent, so the page never renders empty if the API/seeder lags —
 * the same pattern the home sections use. Three stacked sections: full-bleed
 * navy hero, "how we work" numbered steps, and the five value cards. The layout
 * is built with logical properties so the RTL design mirrors to LTR
 * automatically.
 */
@Component({
  selector: 'app-about',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  templateUrl: './about.html',
  styleUrl: './about.scss',
})
export class About {
  private readonly seo = inject(SeoService);
  private readonly translate = inject(TranslateService);
  protected readonly content = inject(AboutContentStore);

  protected readonly steps = [1, 2, 3, 4] as const;
  protected readonly values: ReadonlyArray<{ icon: IconName; key: string }> = [
    { icon: 'shield', key: 'trust' },
    { icon: 'hands', key: 'empower' },
    { icon: 'leaf', key: 'heritage' },
    { icon: 'award', key: 'dignity' },
    { icon: 'spark', key: 'quality' },
  ];

  // `stream` re-emits on language switch, so the SEO tags follow the active
  // language (instant() would freeze the first language's strings).
  private readonly metaTitle = toSignal(this.translate.stream('about.meta_title'));
  private readonly metaDescription = toSignal(
    this.translate.stream('about.meta_description'),
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
