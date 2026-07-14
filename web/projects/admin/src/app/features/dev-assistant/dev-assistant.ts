import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
  viewChild,
  type ElementRef,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { AdminDevAssistantService } from 'data-access';
import { AdminDevAssistantBlock } from './assistant-block';
import { DevAssistantStore } from './dev-assistant.store';

/**
 * The Developer Assistant portal (docs/DEV-ASSISTANT-PORTAL-SPEC.md §4): a chat-style surface over
 * deterministic, metadata-derived answers. Bilingual like the rest of the admin — the chrome
 * follows the language toggle (including RTL) and answers are composed server-side in the active
 * language, while code, paths and tables stay LTR islands.
 */
@Component({
  selector: 'app-admin-dev-assistant',
  imports: [AdminDevAssistantBlock, DatePipe, TranslatePipe],
  templateUrl: './dev-assistant.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDevAssistant {
  private readonly api = inject(AdminDevAssistantService);

  protected readonly store = inject(DevAssistantStore);
  protected readonly capabilities = this.api.capabilitiesResource();
  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly failure = signal(false);

  private readonly transcriptEl =
    viewChild<ElementRef<HTMLElement>>('transcript');

  constructor() {
    // Keep the newest turn in view. No typing animation, no artificial delay — replies are
    // deterministic and near-instant; faking latency would misrepresent the mechanism (FR-UI-6).
    effect(() => {
      this.store.entries();
      this.sending();
      const el = this.transcriptEl()?.nativeElement;
      if (el) {
        requestAnimationFrame(() => el.scrollTo({ top: el.scrollHeight }));
      }
    });
  }

  protected onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.submit(this.draft());
    }
  }

  protected submit(text: string): void {
    const query = text.trim();
    if (!query || this.sending()) {
      return;
    }

    // The rolling follow-up context must be read before this turn is appended (§3.6).
    const contextSubjects = this.store.recentSubjects();

    this.store.addUser(query);
    this.draft.set('');
    this.failure.set(false);
    this.sending.set(true);

    this.api.query({ text: query, contextSubjects }).subscribe({
      next: (reply) => {
        this.store.addAssistant(reply);
        this.sending.set(false);
      },
      error: () => {
        this.failure.set(true);
        this.sending.set(false);
      },
    });
  }
}
