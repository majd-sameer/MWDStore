import { ChangeDetectionStrategy, Component, booleanAttribute, computed, input } from '@angular/core';

export type StatusTone =
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'neutral'
  | 'primary';

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
