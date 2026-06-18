import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CatalogService } from 'data-access';
import { Icon } from 'ui';
import { CategoryLabelPipe } from '../shared/category-label.pipe';

interface FooterLink {
  readonly key: string;
  readonly link: string;
}

interface ShopLink {
  readonly name: string;
  readonly category: string;
}

/**
 * Site footer on the charcoal titanium surface: brand + mission + newsletter
 * capture and the Public Security Directorate endorsement crest, then three
 * link columns (Shop / MadeWithDetermination / Care). All copy keyed through
 * ngx-translate; layout uses logical Bootstrap utilities so it mirrors in RTL.
 */
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, CategoryLabelPipe],
  template: `
    <footer class="site-footer">
      <div class="wrap footer-grid">
        <div class="footer-brand">
          <span class="wordmark">
            <img class="wordmark-logo" src="logo-gold.png" alt="" />
            <span>{{ 'brand.name' | translate }}</span>
          </span>
          <p class="tagline">{{ 'footer.tagline' | translate }}</p>

          <div class="psd">
            <span class="psd-crest">
              <img src="logo-psd.png" [attr.alt]="'footer.psd' | translate" />
            </span>
            <span class="psd-note">{{ 'footer.psd' | translate }}</span>
          </div>

          <form class="newsletter" (submit)="$event.preventDefault()">
            <label class="visually-hidden" for="footer-email">
              {{ 'footer.newsletter' | translate }}
            </label>
            <input
              id="footer-email"
              type="email"
              dir="auto"
              class="form-control"
              [attr.placeholder]="'footer.newsletter' | translate"
            />
            <button
              type="submit"
              class="btn btn-primary"
              [attr.aria-label]="'footer.subscribe' | translate"
            >
              <lib-icon name="arrowEnd" [size]="18" />
            </button>
          </form>
        </div>

        <div class="footer-col">
          <h2 class="footer-h">{{ 'footer.shop' | translate }}</h2>
          <ul>
            <li><a routerLink="/shop">{{ 'nav.shop' | translate }}</a></li>
            @for (item of shopLinks(); track item.category) {
              <li>
                <a [routerLink]="['/shop']" [queryParams]="{ category: item.category }">
                  {{ item.category | categoryLabel: item.name }}
                </a>
              </li>
            }
          </ul>
        </div>

        <div class="footer-col">
          <h2 class="footer-h">{{ 'footer.brand' | translate }}</h2>
          <ul>
            @for (item of brandLinks; track item.key) {
              <li><a [routerLink]="item.link">{{ 'footer.' + item.key | translate }}</a></li>
            }
          </ul>
        </div>

        <div class="footer-col">
          <h2 class="footer-h">{{ 'footer.care' | translate }}</h2>
          <ul>
            @for (item of careLinks; track item.key) {
              <li><a [routerLink]="item.link">{{ 'footer.' + item.key | translate }}</a></li>
            }
          </ul>
        </div>
      </div>

      <div class="wrap footer-base">
        {{ 'footer.rights' | translate: { year: year } }}
      </div>
    </footer>
  `,
  styles: `
    .site-footer {
      background: var(--titanium);
      color: rgba(255, 255, 255, 0.82);
      margin-block-start: 6rem;
      padding-block: 4rem 2rem;
    }
    .footer-grid {
      display: grid;
      grid-template-columns: 1.6fr 1fr 1fr 1fr;
      gap: 2.5rem;
    }
    @media (max-width: 768px) {
      .footer-grid {
        grid-template-columns: 1fr 1fr;
      }
      .footer-brand {
        grid-column: 1 / -1;
      }
    }
    .wordmark {
      display: inline-flex;
      align-items: center;
      gap: 0.65rem;
      font-weight: 700;
      font-size: 1.25rem;
      letter-spacing: -0.01em;
      color: #fff;
    }
    .wordmark-logo {
      block-size: 52px;
      inline-size: auto;
    }
    .tagline {
      margin-block: 0.9rem 1.25rem;
      max-inline-size: 38ch;
      font-size: 0.95rem;
    }
    .psd {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-block-end: 1.5rem;
    }
    .psd-crest {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      inline-size: 64px;
      block-size: 64px;
      padding: 6px;
      border-radius: var(--r-sm);
      background: #fff;
    }
    .psd-crest img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: contain;
    }
    .psd-note {
      font-size: 0.82rem;
      color: rgba(255, 255, 255, 0.65);
      max-inline-size: 26ch;
    }
    .newsletter {
      display: flex;
      gap: 0.5rem;
      max-inline-size: 360px;
    }
    .newsletter .form-control {
      background: rgba(255, 255, 255, 0.08);
      border-color: rgba(255, 255, 255, 0.18);
      color: #fff;
    }
    .newsletter .form-control::placeholder {
      color: rgba(255, 255, 255, 0.55);
    }
    .footer-h {
      font-size: 0.78rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: rgba(255, 255, 255, 0.6);
      margin-block-end: 1rem;
    }
    .footer-col ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }
    .footer-col a {
      color: rgba(255, 255, 255, 0.82);
      text-decoration: none;
      font-size: 0.95rem;
    }
    .footer-col a:hover {
      color: #fff;
    }
    .footer-base {
      margin-block-start: 3rem;
      padding-block-start: 1.5rem;
      border-block-start: 1px solid rgba(255, 255, 255, 0.12);
      font-size: 0.85rem;
      color: rgba(255, 255, 255, 0.55);
    }
  `,
})
export class Footer {
  private readonly catalog = inject(CatalogService);

  protected readonly year = new Date().getFullYear();

  private readonly categories = this.catalog.categoriesResource();

  /** Top-level, in-menu categories from the backend (first four), mirroring the header nav. */
  protected readonly shopLinks = computed<readonly ShopLink[]>(() =>
    (this.categories.value() ?? [])
      .filter((c) => c.includeInMenu && c.parentId === null && c.slug && c.name)
      .sort((a, b) => a.displayOrder - b.displayOrder)
      .slice(0, 4)
      .map((c) => ({ name: c.name as string, category: c.slug as string })),
  );

  protected readonly brandLinks: readonly FooterLink[] = [
    { key: 'about', link: '/' },
    { key: 'makers', link: '/' },
    { key: 'stores', link: '/' },
  ];

  protected readonly careLinks: readonly FooterLink[] = [
    { key: 'delivery_returns', link: '/' },
    { key: 'track', link: '/account/orders' },
    { key: 'contact', link: '/' },
    { key: 'faq', link: '/' },
  ];
}
