import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input } from '@angular/core';

/** Semantic status tone — maps to the shared status tokens (not literal colors). */
export type StatusTone =
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'neutral'
  | 'primary';

/**
 * Status pill — a rounded chip with a leading dot, used for row states
 * (Pending / Open / Closed …). Tone maps to the project's semantic status
 * tokens; the label is projected so callers stay in control of the text.
 *
 * @example
 * <app-status-pill tone="warning">Pending</app-status-pill>
 * <app-status-pill tone="neutral" [dot]="false">Closed</app-status-pill>
 */
@Component({
  selector: 'app-status-pill',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (dot()) {
      <span class="status-pill-dot" aria-hidden="true"></span>
    }
    <ng-content />
  `,
  host: {
    class: 'status-pill',
    '[class]': 'toneClass()',
  },
})
export class StatusPill {
  readonly tone = input<StatusTone>('neutral');
  readonly dot = input(true, { transform: booleanAttribute });

  protected readonly toneClass = computed(() => `status-pill--${this.tone()}`);
}
