import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  signal,
} from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import type { ContentBlockDto } from 'data-access';
import { ToastService } from 'ui';

/**
 * CTA / newsletter band per supported-doc/HOME-PAGE.md §9: ivory rounded card,
 * copy on the left and a white pill subscribe form (email input + green
 * button) with a privacy note on the right. There is no newsletter endpoint
 * yet, so submit just acknowledges with a toast and clears the field.
 * Collapses to one column under 980px.
 *
 * The title and sub copy are admin-editable via the `home.cta` content block
 * (`[block]`), falling back to the original i18n copy when missing.
 */
@Component({
  selector: 'app-cta-band',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <section class="ctawrap">
      <div class="ctaband">
        <div>
          <span class="eyebrow">{{ 'home.cta_eyebrow' | translate }}</span>
          <h2 class="cta-title">{{ block()?.title || ('home.cta_title' | translate) }}</h2>
          <p class="cta-sub">{{ block()?.text || ('home.cta_sub' | translate) }}</p>
        </div>
        <div>
          <form class="subscribe" (submit)="subscribe($event)">
            <input
              type="email"
              required
              [value]="email()"
              (input)="email.set($any($event.target).value)"
              [placeholder]="'home.cta_email' | translate"
              [attr.aria-label]="'home.cta_email_label' | translate"
            />
            <button class="subscribe-btn" type="submit">
              {{ 'home.cta_btn' | translate }}
            </button>
          </form>
          <p class="cta-note">{{ 'home.cta_note' | translate }}</p>
        </div>
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
      padding-block: clamp(48px, 7vw, 84px);
    }
    .ctaband {
      display: grid;
      grid-template-columns: 1.2fr 1fr;
      gap: 40px;
      align-items: center;
      padding: clamp(32px, 5vw, 56px);
      background: var(--surface-2);
      border-radius: var(--r-xl);
    }
    /* Let the grid tracks shrink below their content (the subscribe form)
       instead of forcing the column — and the pill — wider than the card. */
    .ctaband > div {
      min-inline-size: 0;
    }
    @media (max-width: 980px) {
      .ctaband {
        grid-template-columns: 1fr;
        gap: 24px;
      }
    }
    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      font-size: 0.82rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      color: var(--accent);
    }
    .eyebrow::before {
      content: '';
      inline-size: 26px;
      block-size: 2px;
      background: currentColor;
    }
    .cta-title {
      margin-block: 12px 0;
      font-weight: 700;
      font-size: clamp(1.4rem, 3vw, 2rem);
      letter-spacing: -0.02em;
    }
    .cta-sub {
      margin-block: 10px 0;
      color: var(--ink-2);
    }

    .subscribe {
      display: flex;
      gap: 8px;
      padding: 8px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: 999px;
    }
    .subscribe input {
      flex: 1 1 auto;
      min-inline-size: 0;
      border: 0;
      outline: 0;
      background: transparent;
      padding-inline: 14px;
      font: inherit;
      color: var(--ink);
    }
    .subscribe-btn {
      flex: 0 0 auto;
      border: 0;
      border-radius: 999px;
      padding: 11px 26px;
      background: var(--green);
      color: #fff;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s ease;
    }
    .subscribe-btn:hover {
      background: var(--green-strong);
    }
    .cta-note {
      margin-block: 10px 0;
      font-size: 0.82rem;
      color: var(--ink-2);
    }
  `,
})
export class CtaBand {
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  /** The `home.cta` content block, or null when missing/unpublished (falls back to i18n). */
  readonly block = input<ContentBlockDto | null>(null);

  protected readonly email = signal('');

  protected subscribe(event: Event): void {
    event.preventDefault();
    if (!this.email().trim()) {
      return;
    }
    this.toast.success(this.translate.instant('home.cta_thanks'));
    this.email.set('');
  }
}
