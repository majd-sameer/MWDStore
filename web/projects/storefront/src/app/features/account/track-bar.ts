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
  templateUrl: './track-bar.html',
  styleUrl: './track-bar.scss',
})
export class TrackBar {
  readonly status = input.required<number>();

  protected readonly stages = TRACK_STAGES;
  protected readonly current = computed(() => stageIndex(this.status()));
  protected readonly cancelled = computed(() => isCancelled(this.status()));
}
