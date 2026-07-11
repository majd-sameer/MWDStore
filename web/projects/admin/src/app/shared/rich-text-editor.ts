import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  input,
  model,
  signal,
  viewChild,
} from '@angular/core';
import type { FormValueControl } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';

/** One toolbar button: the `execCommand` name, its icon and its label i18n key. */
interface ToolbarButton {
  command: string;
  icon: string;
  label: string;
}

const TOOLBAR: readonly ToolbarButton[] = [
  { command: 'bold', icon: 'bi-type-bold', label: 'editor.bold' },
  { command: 'italic', icon: 'bi-type-italic', label: 'editor.italic' },
  { command: 'underline', icon: 'bi-type-underline', label: 'editor.underline' },
  { command: 'insertUnorderedList', icon: 'bi-list-ul', label: 'editor.bullet_list' },
  { command: 'insertOrderedList', icon: 'bi-list-ol', label: 'editor.numbered_list' },
];

/** `innerHTML` values a "cleared" contenteditable leaves behind — treated as empty. */
const EMPTY_HTML = new Set(['', '<br>', '<div><br></div>', '<p><br></p>']);

/**
 * True when the value is a complete HTML document (`<!DOCTYPE …>` / `<html>`) rather
 * than a fragment. A contenteditable cannot hold one faithfully (the browser strips
 * `<html>`/`<head>` on parse), so such values are locked to the source view.
 */
export function isFullHtmlDocument(html: string): boolean {
  const head = html.trimStart().slice(0, 15).toLowerCase();
  return head.startsWith('<!doctype') || head.startsWith('<html');
}

/**
 * Lightweight rich-text editor — a **Signal Forms custom control**
 * ({@link FormValueControl}) that binds to a plain HTML `string` field exactly like
 * a native input, so it drops into a form with `[formField]`:
 *
 * ```html
 * <rich-text-editor [formField]="f.body" dir="rtl" />
 * ```
 *
 * It edits one language at a time (pair two of them for bilingual copy). The value
 * is HTML produced by `document.execCommand`, or — via the `</>` toolbar toggle — raw
 * HTML typed/pasted into a source textarea (for authors bringing pre-made article
 * markup; pasting HTML source into the WYSIWYG area would store it escaped). Complete
 * HTML documents (`<!DOCTYPE …>`) are accepted too and stay locked to the source view;
 * the storefront renders those in a sandboxed iframe and everything else as trusted
 * inline HTML. There is no editor dependency — this is the admin app (never
 * server-rendered), so the browser `contenteditable`/`execCommand` APIs are always
 * available.
 */
@Component({
  selector: 'rich-text-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  templateUrl: './rich-text-editor.html',
  styleUrl: './rich-text-editor.scss',
})
export class RichTextEditor implements FormValueControl<string> {
  /** The bound HTML string. Kept in sync by the `[formField]` directive, or `[(value)]`. */
  readonly value = model<string>('');

  /** Disabled state — bound automatically from the field when used with `[formField]`. */
  readonly disabled = input(false);
  /** Validity + touched companion inputs (populated by the `Field` directive). */
  readonly invalid = input(false);
  readonly touched = input(false);

  /** Writing direction of the editable area (`rtl` for Arabic copy). */
  readonly dir = input<'ltr' | 'rtl'>('ltr');
  /** Placeholder shown while the area is empty. */
  readonly placeholder = input('');
  /** `id` for the editable area, so an outer `<label for>` can target it. */
  readonly controlId = input<string | null>(null);

  protected readonly toolbar = TOOLBAR;

  /** When true the raw-HTML source textarea replaces the WYSIWYG area. */
  protected readonly sourceMode = signal(false);

  /** Full HTML documents can only be edited as source — the toggle is locked. */
  protected readonly fullDocument = computed(() => isFullHtmlDocument(this.value() ?? ''));

  private readonly area = viewChild<ElementRef<HTMLElement>>('area');

  constructor() {
    // A full-document value cannot round-trip through contenteditable — force the
    // source view whenever one is loaded (e.g. editing an article saved as full HTML).
    effect(() => {
      if (this.fullDocument()) {
        this.sourceMode.set(true);
      }
    });
    // Push external value changes into the DOM, but never while the caret is inside:
    // only overwrite when the incoming HTML actually differs from what's shown, so
    // typing (which already updates `value` via onInput) doesn't reset the cursor.
    effect(() => {
      const ref = this.area();
      if (!ref) {
        return;
      }
      const incoming = this.value() ?? '';
      if (ref.nativeElement.innerHTML !== incoming) {
        ref.nativeElement.innerHTML = incoming;
      }
    });
  }

  /** Read the edited HTML back into the model, normalising the "empty" shapes to ''. */
  protected onInput(): void {
    const html = this.area()?.nativeElement.innerHTML ?? '';
    this.value.set(EMPTY_HTML.has(html) ? '' : html);
  }

  /** Flip between the WYSIWYG area and the raw-HTML source textarea. */
  protected toggleSource(): void {
    // A full document must stay in source view — the WYSIWYG area would mangle it.
    if (this.disabled() || this.fullDocument()) {
      return;
    }
    // Leaving source mode re-creates the contenteditable; the constructor effect
    // then pushes the (possibly edited) value into it.
    this.sourceMode.update((v) => !v);
  }

  /** Read the source textarea back into the model. */
  protected onSourceInput(event: Event): void {
    const raw = (event.target as HTMLTextAreaElement).value;
    this.value.set(EMPTY_HTML.has(raw.trim()) ? '' : raw);
  }

  /** Run a formatting command on the current selection, then sync the model. */
  protected exec(command: string): void {
    if (this.disabled()) {
      return;
    }
    this.area()?.nativeElement.focus();
    document.execCommand(command, false);
    this.onInput();
  }

  /** Wrap the selection in a link (prompts for the URL); a blank prompt is a no-op. */
  protected addLink(): void {
    if (this.disabled()) {
      return;
    }
    const url = prompt(this.placeholder() || 'https://');
    if (!url) {
      return;
    }
    this.area()?.nativeElement.focus();
    document.execCommand('createLink', false, url);
    this.onInput();
  }

  /** Strip inline formatting (and any link) from the selection. */
  protected clearFormat(): void {
    if (this.disabled()) {
      return;
    }
    this.area()?.nativeElement.focus();
    document.execCommand('removeFormat', false);
    document.execCommand('unlink', false);
    this.onInput();
  }
}
