import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  model,
  signal,
} from '@angular/core';
import type { FormValueControl } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from 'core';

/** The bilingual value edited by {@link MultiLangInput}: Arabic + English copy. */
export interface MultiLangValue {
  ar: string;
  en: string;
}

function emptyValue(): MultiLangValue {
  return { ar: '', en: '' };
}

/**
 * Bilingual (Arabic / English) text control — a drop-in replacement for a plain
 * `<input type="text">` or `<textarea>` anywhere admin copy needs both languages.
 *
 * It is a **Signal Forms custom control** (implements {@link FormValueControl}),
 * so it binds to a `{ ar, en }` field exactly like a native input:
 *
 * ```html
 * <!-- reactive: bind a Signal-Forms field of type MultiLangValue -->
 * <multi-lang-input [formField]="f.title" />
 * <multi-lang-input type="textarea" [rows]="4" [formField]="f.body" />
 *
 * <!-- standalone: two-way bind the value model directly -->
 * <multi-lang-input [(value)]="title" />
 * ```
 *
 * The inline field edits the **current UI language** (Arabic when the console is
 * in Arabic, English otherwise). A globe button opens a modal exposing both the
 * Arabic and English fields together, so the second language is always reachable
 * without leaving the form. Modal edits are committed on Save and discarded on
 * Cancel; inline edits commit immediately.
 */
@Component({
  selector: 'multi-lang-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  templateUrl: './multi-lang-input.html',
  styleUrl: './multi-lang-input.scss',
})
export class MultiLangInput implements FormValueControl<MultiLangValue> {
  private readonly language = inject(LanguageService);

  /** The bound value. Kept in sync by the `[formField]` directive, or `[(value)]`. */
  readonly value = model<MultiLangValue>(emptyValue());

  /** Disabled state — bound automatically from the field when used with `[formField]`. */
  readonly disabled = input(false);

  /** Render an `<input>` (single line) or a `<textarea>` (multi-line). */
  readonly type = input<'text' | 'textarea'>('text');
  /** Rows for the `textarea` variant. */
  readonly rows = input(3);
  /** Placeholder for the inline field. */
  readonly placeholder = input('');
  /** `id` for the inline field, so an outer `<label for>` can target it. */
  readonly controlId = input<string | null>(null);
  /** Marks the inline field invalid (mirrors the field's error state). */
  readonly invalid = input(false);

  /** The language edited inline: whichever the console is currently showing. */
  protected readonly primaryLang = computed<'ar' | 'en'>(() =>
    this.language.lang() === 'en' ? 'en' : 'ar',
  );
  /** The other language — the one only reachable through the modal. */
  protected readonly secondaryLang = computed<'ar' | 'en'>(() =>
    this.primaryLang() === 'ar' ? 'en' : 'ar',
  );

  /** Value shown in the inline field. */
  protected readonly inlineValue = computed(() => this.value()[this.primaryLang()]);
  /** True when the hidden (secondary) language already has copy — lights up the globe. */
  protected readonly hasSecondary = computed(
    () => this.value()[this.secondaryLang()].trim().length > 0,
  );

  protected readonly modalOpen = signal(false);
  /** Working copy while the modal is open; committed to `value` only on Save. */
  protected readonly draft = signal<MultiLangValue>(emptyValue());

  /** Inline edit → commit immediately to the primary language. */
  protected setInline(event: Event): void {
    const text = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this.value.update((v) => ({ ...v, [this.primaryLang()]: text }));
  }

  protected openModal(): void {
    this.draft.set({ ...this.value() });
    this.modalOpen.set(true);
  }

  protected closeModal(): void {
    this.modalOpen.set(false);
  }

  protected setDraft(lang: 'ar' | 'en', event: Event): void {
    const text = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this.draft.update((v) => ({ ...v, [lang]: text }));
  }

  protected applyModal(): void {
    this.value.set({ ...this.draft() });
    this.modalOpen.set(false);
  }
}
