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

export interface MultiLangValue {
  ar: string;
  en: string;
}

function emptyValue(): MultiLangValue {
  return { ar: '', en: '' };
}


@Component({
  selector: 'multi-lang-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  templateUrl: './multi-lang-input.html',
  styleUrl: './multi-lang-input.scss',
})
export class MultiLangInput implements FormValueControl<MultiLangValue> {
  private readonly language = inject(LanguageService);

  readonly value = model<MultiLangValue>(emptyValue());

  readonly disabled = input(false);
  readonly invalid = input(false);
  readonly touched = input(false);

  readonly type = input<'text' | 'textarea'>('text');
  readonly rows = input(3);
  readonly placeholder = input('');
  readonly controlId = input<string | null>(null);

  protected readonly primaryLang = computed<'ar' | 'en'>(() =>
    this.language.lang() === 'en' ? 'en' : 'ar',
  );
  protected readonly secondaryLang = computed<'ar' | 'en'>(() =>
    this.primaryLang() === 'ar' ? 'en' : 'ar',
  );

  protected readonly inlineValue = computed(() => this.value()[this.primaryLang()]);
  protected readonly hasSecondary = computed(
    () => this.value()[this.secondaryLang()].trim().length > 0,
  );

  protected readonly modalOpen = signal(false);
  protected readonly draft = signal<MultiLangValue>(emptyValue());

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
