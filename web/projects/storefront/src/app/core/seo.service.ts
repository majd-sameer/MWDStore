import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { Router } from '@angular/router';

export interface SeoMetadata {
  title: string;
  description?: string;
  /** Open Graph type, e.g. `'website'` (default) or `'product'`. */
  type?: string;
  /** Absolute or root-relative image URL for social cards. */
  image?: string;
}

const SITE_NAME = 'MadeWithDetermination';

/**
 * Centralizes per-page SEO: document title, meta description, Open Graph /
 * Twitter cards, and a canonical link. Pages call `update()` (typically inside
 * an `effect` driven by their data resource) so the tags are present in the
 * server-rendered HTML.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  update(metadata: SeoMetadata): void {
    const pageTitle = `${metadata.title} · ${SITE_NAME}`;
    this.title.setTitle(pageTitle);

    this.meta.updateTag({ property: 'og:title', content: pageTitle });
    this.meta.updateTag({ property: 'og:site_name', content: SITE_NAME });
    this.meta.updateTag({ property: 'og:type', content: metadata.type ?? 'website' });
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ property: 'og:url', content: this.absoluteUrl() });

    if (metadata.description) {
      const description = metadata.description.slice(0, 300);
      this.meta.updateTag({ name: 'description', content: description });
      this.meta.updateTag({ property: 'og:description', content: description });
      this.meta.updateTag({ name: 'twitter:description', content: description });
    } else {
      this.meta.removeTag('name="description"');
    }

    if (metadata.image) {
      this.meta.updateTag({ property: 'og:image', content: metadata.image });
      this.meta.updateTag({ name: 'twitter:image', content: metadata.image });
    }

    this.setCanonical(this.absoluteUrl());
  }

  private absoluteUrl(): string {
    // `document.location` reflects the request URL during SSR and the browser
    // URL on the client; fall back to the router path if unavailable.
    const origin = this.document.location?.origin ?? '';
    return `${origin}${this.router.url.split('?')[0]}`;
  }

  private setCanonical(href: string): void {
    let link = this.document.head.querySelector<HTMLLinkElement>(
      'link[rel="canonical"]',
    );
    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.document.head.appendChild(link);
    }
    link.setAttribute('href', href);
  }
}
