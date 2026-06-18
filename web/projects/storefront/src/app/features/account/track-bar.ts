import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon } from 'ui';
import { isCancelled, stageIndex, TRACK_STAGES } from './order-status';

/**
 * Order-status timeline: four logical-flow steps (Placed → Processing →
 * Shipped → Delivered) with the reached steps filled. Derives its state from
 * the numeric status code, so it stays correct in any language; shows a
 * cancelled state for cancelled/refunded/closed orders.
 */
@Component({
  selector: 'app-track-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Icon],
  template: `
    @if (cancelled()) {
      <div class="cancelled">
        <lib-icon name="x" [size]="16" />
        {{ 'track.cancelled' | translate }}
      </div>
    } @else {
      <ol class="track">
        @for (stage of stages; track stage; let i = $index) {
          <li class="step" [class.done]="i <= current()" [class.current]="i === current()">
            <span class="dot">
              @if (i < current()) {
                <lib-icon name="check" [size]="12" />
              }
            </span>
            <span class="label">{{ 'track.' + stage | translate }}</span>
          </li>
        }
      </ol>
    }
  `,
  styles: `
    .track {
      display: flex;
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .step {
      flex: 1 1 0;
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.78rem;
      color: var(--ink-3);
      text-align: center;
    }
    /* connector line between dots, drawn on the start side of each step */
    .step::before {
      content: '';
      position: absolute;
      inset-block-start: 9px;
      inset-inline-end: 50%;
      inline-size: 100%;
      block-size: 2px;
      background: var(--line);
    }
    .step:first-child::before {
      display: none;
    }
    .step.done::before {
      background: var(--accent);
    }
    .dot {
      position: relative;
      z-index: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 20px;
      block-size: 20px;
      border-radius: 50%;
      background: var(--surface-3);
      color: var(--accent-ink);
    }
    .step.done .dot {
      background: var(--accent);
    }
    .step.current .label,
    .step.done .label {
      color: var(--ink);
      font-weight: 600;
    }
    .cancelled {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      color: var(--danger, #d6455d);
      font-weight: 600;
      font-size: 0.9rem;
    }
  `,
})
export class TrackBar {
  readonly status = input.required<number>();

  protected readonly stages = TRACK_STAGES;
  protected readonly current = computed(() => stageIndex(this.status()));
  protected readonly cancelled = computed(() => isCancelled(this.status()));
}
