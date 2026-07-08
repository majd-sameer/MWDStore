import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import {
  AdminContentBlocksService,
  AdminMediaService,
  type AdminContentSection,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

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
  address: 'cms.keys.address',
  map: 'cms.keys.map',
  // FAQ page — the q1/a1 … item keys read fine raw.
  subtitle: 'cms.keys.subtitle',
};

/**
 * Site Content editor: edit the words and images of designed storefront sections without touching
 * layout. Each sidebar link loads one page's sections (cards); each block is a labeled field by type
 * (text/richtext → bilingual `<multi-lang-input>`, image → picker, link → URL). Saves per section.
 * Keys and type are code-owned server-side, so this only ever changes content.
 */
@Component({
  selector: 'app-admin-content-blocks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './content-blocks.html',
})
export class AdminContentBlocks {
  private readonly service = inject(AdminContentBlocksService);
  private readonly media = inject(AdminMediaService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly route = inject(ActivatedRoute);

  /** Page being edited, taken from the `:page` route segment (each sidebar link targets one). */
  protected readonly page = toSignal(
    this.route.paramMap.pipe(map((p) => p.get('page') ?? 'home')),
    { initialValue: 'home' },
  );
  protected readonly list = this.service.listResource(this.page);
  protected readonly savingSection = signal<string | null>(null);
  protected readonly uploadingId = signal<number | null>(null);
  protected readonly edits = signal<Record<number, BlockEdit>>({});

  /** The FAQ list is the one repeatable section: questions can be added/removed here. */
  protected readonly isFaq = computed(() => this.page() === 'faq');
  protected readonly newQuestion = signal<{ question: MultiLangValue; answer: MultiLangValue }>({
    question: { ar: '', en: '' },
    answer: { ar: '', en: '' },
  });
  protected readonly addingQuestion = signal(false);

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

  /** Both-language value of a text block, for `<multi-lang-input [value]>`. */
  protected multiValue(id: number): MultiLangValue {
    const e = this.edit(id);
    return { ar: e.valueAr, en: e.valueEn };
  }

  /** `<multi-lang-input (valueChange)>` → write both languages back into edit state. */
  protected setMultiLang(id: number, value: MultiLangValue): void {
    this.patch(id, { valueAr: value.ar, valueEn: value.en });
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

  /** True for FAQ question blocks (`q1`, `q2`, …) — the ones that carry a "remove" action. */
  protected isFaqQuestion(blockKey: string): boolean {
    return /^q\d+$/.test(blockKey);
  }

  protected setNewQuestion(field: 'question' | 'answer', value: MultiLangValue): void {
    this.newQuestion.update((q) => ({ ...q, [field]: value }));
  }

  protected addQuestion(): void {
    const q = this.newQuestion();
    if (!q.question.ar.trim() && !q.question.en.trim()) {
      this.toast.error(this.translate.instant('cms.faq.need_question'));
      return;
    }
    this.addingQuestion.set(true);
    this.service
      .addFaqQuestion({
        questionAr: q.question.ar || null,
        questionEn: q.question.en || null,
        answerAr: q.answer.ar || null,
        answerEn: q.answer.en || null,
      })
      .subscribe({
        next: () => {
          this.newQuestion.set({ question: { ar: '', en: '' }, answer: { ar: '', en: '' } });
          this.reloadBlocks();
          this.toast.success(this.translate.instant('cms.faq.added'));
          this.addingQuestion.set(false);
        },
        error: () => {
          this.toast.error(this.translate.instant('cms.faq.add_failed'));
          this.addingQuestion.set(false);
        },
      });
  }

  protected deleteQuestion(blockKey: string): void {
    if (!confirm(this.translate.instant('cms.faq.confirm_delete'))) {
      return;
    }
    const index = Number(blockKey.slice(1));
    this.service.deleteFaqQuestion(index).subscribe({
      next: () => {
        this.reloadBlocks();
        this.toast.success(this.translate.instant('cms.faq.deleted'));
      },
      error: () => this.toast.error(this.translate.instant('cms.faq.delete_failed')),
    });
  }

  /** Reload the section list and force the editable copy to reseed (so new blocks get fields). */
  private reloadBlocks(): void {
    this.seededFor = '';
    this.list.reload();
  }
}
