import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CatalogService } from 'data-access';
import { Icon, type IconName } from 'ui';
import { CategoryLabelPipe } from '../shared/category-label.pipe';
import { FooterContentStore } from '../core/footer-content.store';

interface FooterLink {
  readonly key: string;
  readonly link: string;
}

interface ShopLink {
  readonly name: string;
  readonly category: string;
}

interface SocialLink {
  /** BlockKey under the `footer-social` section; also selects the icon. */
  readonly key: string;
  readonly icon: IconName;
  readonly label: string;
}

/**
 * Site footer on the charcoal titanium surface: brand + mission + newsletter
 * capture and the Public Security Directorate endorsement crest, then three
 * link columns (Shop / MadeWithDetermination / Care) and a social row. Editable
 * copy reads the `footer` content blocks (CMS) and falls back to ngx-translate;
 * social icons render only for platforms an admin has given a URL. Layout uses
 * logical Bootstrap utilities so it mirrors in RTL.
 */
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon, CategoryLabelPipe],
  templateUrl: './footer.html',
  styleUrl: './footer.scss',
})
export class Footer {
  private readonly catalog = inject(CatalogService);
  protected readonly content = inject(FooterContentStore);

  protected readonly year = new Date().getFullYear();

  /** Fixed social platforms; each renders only when its `footer-social` block carries a URL. */
  protected readonly socials: readonly SocialLink[] = [
    { key: 'facebook', icon: 'facebook', label: 'Facebook' },
    { key: 'instagram', icon: 'instagram', label: 'Instagram' },
    { key: 'twitter', icon: 'twitter', label: 'X (Twitter)' },
    { key: 'youtube', icon: 'youtube', label: 'YouTube' },
    { key: 'tiktok', icon: 'tiktok', label: 'TikTok' },
    { key: 'whatsapp', icon: 'whatsapp', label: 'WhatsApp' },
  ];

  /** True once at least one social platform has a URL configured in the CMS. */
  protected readonly hasSocial = computed(() =>
    this.socials.some((s) => !!this.content.block('footer-social', s.key)?.linkUrl),
  );

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
