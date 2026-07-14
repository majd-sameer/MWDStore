import { TestBed } from '@angular/core/testing';
import type { DevAssistantReply } from 'data-access';
import { DevAssistantStore } from './dev-assistant.store';

function reply(subject: string | null): DevAssistantReply {
  return {
    intent: 'SchemaQuery',
    subject,
    hit: subject !== null,
    subjectCarriedOver: false,
    fingerprint: { assemblyVersion: '1.0.0', modelHash: 'abc123', builtAt: '2026-01-01T00:00:00Z' },
    blocks: [],
  };
}

describe('DevAssistantStore', () => {
  let store: DevAssistantStore;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({});
    store = TestBed.inject(DevAssistantStore);
  });

  it('appends user and assistant turns in order', () => {
    store.addUser('Show the columns of Category');
    store.addAssistant(reply('Category'));

    const entries = store.entries();
    expect(entries.length).toBe(2);
    expect(entries[0].role).toBe('user');
    expect(entries[1].reply?.subject).toBe('Category');
  });

  it('exposes at most the last three resolved subjects for follow-up context', () => {
    for (const subject of ['Category', 'Brand', null, 'Order', 'Product']) {
      store.addAssistant(reply(subject));
    }
    expect(store.recentSubjects()).toEqual(['Brand', 'Order', 'Product']);
  });

  it('tracks checklist progress per entry and block', () => {
    store.addAssistant(reply('Category'));
    const entry = store.entries()[0];

    store.toggleStep(entry.id, 0, 2);
    store.toggleStep(entry.id, 0, 1);
    expect([...store.checkedSteps(store.entries()[0], 0)]).toEqual([1, 2]);

    store.toggleStep(entry.id, 0, 2);
    expect([...store.checkedSteps(store.entries()[0], 0)]).toEqual([1]);
  });

  it('clears the transcript', () => {
    store.addUser('help');
    store.clear();
    expect(store.isEmpty()).toBe(true);
  });
});
