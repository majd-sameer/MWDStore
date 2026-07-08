import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
} from '@angular/core';
import {
  AdminContentBlocksService,
  AdminMediaService,
  type AdminContentSection,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/** Local editable state for one block (both languages + media/link/active). */
interface BlockEdit {
  valueAr: string;
  valueEn: string;
  linkUrl: string;
  isActive: boolean;
  mediumId: number | null;
  mediaUrl: string | null;
}

/** Human labels for known block keys; unknown keys fall back to the raw key. */
const KEY_LABELS: Record<string, string> = {
  'hero-copy.title': 'cms.keys.hero_title',
  'hero-copy.subtitle': 'cms.keys.hero_subtitle',
  'hero-copy.cta-label': 'cms.keys.hero_cta_label',
  'hero-copy.cta': 'cms.keys.hero_cta',
  'hero-media': 'cms.keys.hero_media',
  'mission.title': 'cms.keys.mission_title',
  'cta.title': 'cms.keys.cta_title',
  // About page — shared generic slots; the step/value item keys read fine raw.
  eyebrow: 'cms.keys.eyebrow',
  title: 'cms.keys.title',
  body: 'cms.keys.body',
  'cta-label': 'cms.keys.cta_label',
  // Footer — the social platform keys (facebook, instagram, …) read fine raw.
  heading: 'cms.keys.heading',
  tagline: 'cms.keys.tagline',
  psd: 'cms.keys.psd',
  newsletter: 'cms.keys.newsletter',
  about: 'cms.keys.link_about',
  makers: 'cms.keys.link_makers',
  stores: 'cms.keys.link_stores',
  delivery_returns: 'cms.keys.link_delivery',
  track: 'cms.keys.link_track',
  contact: 'cms.keys.link_contact',
  faq: 'cms.keys.link_faq',
};

/**
 * Site Content editor: edit the words and images of designed storefront sections without touching
 * layout. A page selector loads its sections (cards); each block is a labeled field by type
 * (text/richtext → textarea, image → picker, link → URL), with an AR|EN toggle for text. Saves per
 * section. Keys and type are code-owned server-side, so this only ever changes content.
 */
@Component({
  selector: 'app-admin-content-blocks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader],
  templateUrl: './content-blocks.html',
})
export class AdminContentBlocks {
  private readonly service = inject(AdminContentBlocksService);
  private readonly media = inject(AdminMediaService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly pages = ['home', 'about', 'footer'];
  protected readonly page = signal('home');
  protected readonly lang = signal<'ar' | 'en'>('ar');

  protected readonly list = this.service.listResource(this.page);
  protected readonly savingSection = signal<string | null>(null);
  protected readonly uploadingId = signal<number | null>(null);
  protected readonly edits = signal<Record<number, BlockEdit>>({});

  private seededFor = '';

  constructor() {
    // Seed the editable copy from the loaded sections once per page.
    effect(() => {
      const sections = this.list.value();
      if (!sections || this.seededFor === this.page()) {
        return;
      }
      const map: Record<number, BlockEdit> = {};
      for (const section of sections) {
        for (const block of section.blocks) {
          map[block.id] = {
            valueAr: block.valueAr ?? '',
            valueEn: block.valueEn ?? '',
            linkUrl: block.linkUrl ?? '',
            isActive: block.isActive,
            mediumId: block.mediumId,
            mediaUrl: block.mediaUrl,
          };
        }
      }
      this.edits.set(map);
      this.seededFor = this.page();
    });
  }

  protected edit(id: number): BlockEdit {
    return this.edits()[id] ?? { valueAr: '', valueEn: '', linkUrl: '', isActive: true, mediumId: null, mediaUrl: null };
  }

  protected label(blockKey: string): string {
    const key = KEY_LABELS[blockKey];
    return key ? this.translate.instant(key) : blockKey;
  }

  private patch(id: number, patch: Partial<BlockEdit>): void {
    this.edits.update((map) => ({ ...map, [id]: { ...this.edit(id), ...patch } }));
  }

  protected setText(id: number, event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this.patch(id, this.lang() === 'ar' ? { valueAr: value } : { valueEn: value });
  }

  protected setLink(id: number, event: Event): void {
    this.patch(id, { linkUrl: (event.target as HTMLInputElement).value });
  }

  protected setActive(id: number, event: Event): void {
    this.patch(id, { isActive: (event.target as HTMLInputElement).checked });
  }

  protected onImageSelected(id: number, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    this.uploadingId.set(id);
    this.media.upload(file).subscribe({
      next: (m) => {
        this.patch(id, { mediumId: m.id, mediaUrl: m.url });
        this.uploadingId.set(null);
      },
      error: () => {
        this.toast.error(this.translate.instant('cms.upload_failed'));
        this.uploadingId.set(null);
      },
    });
  }

  protected saveSection(section: AdminContentSection): void {
    this.savingSection.set(section.sectionKey);
    const calls = section.blocks.map((b) => {
      const e = this.edit(b.id);
      return firstValueFrom(
        this.service.update(b.id, {
          value: e.valueAr || null,
          valueEn: e.valueEn || null,
          mediumId: e.mediumId,
          linkUrl: e.linkUrl || null,
          isActive: e.isActive,
        }),
      );
    });
    void Promise.all(calls)
      .then(() => this.toast.success(this.translate.instant('cms.saved')))
      .catch(() => this.toast.error(this.translate.instant('cms.save_failed')))
      .finally(() => this.savingSection.set(null));
  }

  protected setPage(event: Event): void {
    this.page.set((event.target as HTMLSelectElement).value);
  }
}
