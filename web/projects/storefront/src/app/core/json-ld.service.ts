import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';

/** A schema.org JSON-LD object (loosely typed — callers build the concrete shape). */
export type JsonLdSchema = Record<string, unknown>;

/**
 * Writes/removes `<script type="application/ld+json">` tags in `<head>`, keyed by an id so a
 * page can hold more than one at once (e.g. `product` + `breadcrumb`) without clobbering each
 * other, and so a page that no longer applies (e.g. navigating away) can clean up after itself.
 *
 * Call `set()` from the same `effect()` that drives `SeoService.update()` so the JSON-LD is
 * present in the server-rendered HTML — crawlers see it without running client JS.
 */
@Injectable({ providedIn: 'root' })
export class JsonLdService {
  private readonly document = inject(DOCUMENT);

  set(id: string, schema: JsonLdSchema): void {
    const scriptId = `ld-json-${id}`;
    let script = this.document.getElementById(scriptId) as HTMLScriptElement | null;
    if (!script) {
      script = this.document.createElement('script');
      script.type = 'application/ld+json';
      script.id = scriptId;
      this.document.head.appendChild(script);
    }
    script.textContent = JSON.stringify({ '@context': 'https://schema.org', ...schema });
  }

  remove(id: string): void {
    this.document.getElementById(`ld-json-${id}`)?.remove();
  }
}
