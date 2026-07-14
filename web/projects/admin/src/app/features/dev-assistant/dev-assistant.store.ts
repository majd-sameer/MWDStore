import { Injectable, computed, effect, signal } from '@angular/core';
import type { DevAssistantReply } from 'data-access';

/**
 * One transcript row. Assistant entries carry the structured reply plus the purely-visual
 * checklist progress state (`checked`: block index → checked step indexes, FR-UI-10).
 */
export interface DevAssistantTranscriptEntry {
  id: number;
  role: 'user' | 'assistant';
  text?: string;
  reply?: DevAssistantReply;
  checked?: Record<number, number[]>;
}

const STORAGE_KEY = 'mws_dev_assistant_transcript';

/**
 * Client-side-only transcript state (FR-UI-8): a signal store persisted to `sessionStorage`
 * (survives route changes and reloads within the tab, dies with the tab). Nothing conversational
 * is ever written to localStorage or the server.
 */
@Injectable({ providedIn: 'root' })
export class DevAssistantStore {
  private readonly _entries = signal<readonly DevAssistantTranscriptEntry[]>(load());
  private nextId =
    this._entries().reduce((max, entry) => Math.max(max, entry.id), 0) + 1;

  readonly entries = this._entries.asReadonly();
  readonly isEmpty = computed(() => this._entries().length === 0);

  /** The resolved subjects of the last assistant turns, oldest first — the §3.6 follow-up context. */
  readonly recentSubjects = computed(() =>
    this._entries()
      .filter((entry) => entry.role === 'assistant' && entry.reply?.subject)
      .slice(-3)
      .map((entry) => entry.reply!.subject!),
  );

  constructor() {
    effect(() => persist(this._entries()));
  }

  addUser(text: string): void {
    this.append({ id: this.nextId++, role: 'user', text });
  }

  addAssistant(reply: DevAssistantReply): void {
    this.append({ id: this.nextId++, role: 'assistant', reply, checked: {} });
  }

  toggleStep(entryId: number, blockIndex: number, stepIndex: number): void {
    this._entries.update((entries) =>
      entries.map((entry) => {
        if (entry.id !== entryId) return entry;
        const checked = { ...(entry.checked ?? {}) };
        const steps = new Set(checked[blockIndex] ?? []);
        if (steps.has(stepIndex)) {
          steps.delete(stepIndex);
        } else {
          steps.add(stepIndex);
        }
        checked[blockIndex] = [...steps].sort((a, b) => a - b);
        return { ...entry, checked };
      }),
    );
  }

  checkedSteps(entry: DevAssistantTranscriptEntry, blockIndex: number): ReadonlySet<number> {
    return new Set(entry.checked?.[blockIndex] ?? []);
  }

  clear(): void {
    this._entries.set([]);
  }

  private append(entry: DevAssistantTranscriptEntry): void {
    this._entries.update((entries) => [...entries, entry]);
  }
}

function load(): readonly DevAssistantTranscriptEntry[] {
  try {
    const raw = globalThis.sessionStorage?.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as DevAssistantTranscriptEntry[]) : [];
  } catch {
    return [];
  }
}

function persist(entries: readonly DevAssistantTranscriptEntry[]): void {
  try {
    globalThis.sessionStorage?.setItem(STORAGE_KEY, JSON.stringify(entries));
  } catch {
    // Storage may be unavailable (private mode quota); the transcript then lives in memory only.
  }
}
