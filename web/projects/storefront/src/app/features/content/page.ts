import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { StorefrontFeaturesService, type PublicPageDto } from 'data-access';
import { SeoService } from '../../core/seo.service';

/**
 * CMS page renderer (`/pages/:slug`): server-rendered, body is trusted admin-authored HTML
 * (same trust model as the old Razor CMS pages).
 */
@Component({
  selector: 'app-cms-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, RouterLink],
  template: `
    <div class="container py-4 cms-page">
      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      } @else if (page(); as p) {
        <h1 class="page-title">{{ p.name }}</h1>
        <div [innerHTML]="p.body"></div>
      } @else {
        <div class="text-center py-5">
          <p class="text-body-secondary">{{ 'common.error' | translate }}</p>
          <a routerLink="/">{{ 'common.home' | translate }}</a>
        </div>
      }
    </div>
  `,
  styles: `
    .page-title {
      font-size: 1.8rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      margin-block-end: 1.25rem;
    }
    .cms-page {
      max-inline-size: 56rem;
    }
  `,
})
export class CmsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(StorefrontFeaturesService);
  private readonly seo = inject(SeoService);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly page = signal<PublicPageDto | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    effect(() => {
      const slug = this.params().get('slug');
      if (!slug) {
        return;
      }
      this.loading.set(true);
      this.service.page(slug).subscribe({
        next: (page) => {
          this.page.set(page);
          this.loading.set(false);
          this.seo.update({
            title: page.metaTitle || page.name || slug,
            description: page.metaDescription ?? undefined,
          });
        },
        error: () => {
          this.page.set(null);
          this.loading.set(false);
        },
      });
    });
  }
}
