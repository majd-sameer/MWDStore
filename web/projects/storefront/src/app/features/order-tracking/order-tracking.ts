import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import {
  OrderService,
  type OrderTrackingDto,
  type OrderTrackingEventDto,
} from 'data-access';
import { Button, Icon, type IconName } from 'ui';
import { OrderDetailView } from '../../shared/order-detail-view';
import {
  isCancelled,
  stageIndex,
  statusLabel,
  TRACK_STAGES,
} from '../account/order-status';

/**
 * Public "track your order" page (no sign-in), styled like a courier tracking page:
 * a current-status hero, a four-step progress bar, and a dated timeline of milestones.
 * A shopper enters their order number and the email it was placed under; the backend
 * only returns the status when both match, so orders can't be enumerated.
 */
@Component({
  selector: 'app-order-tracking',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, DatePipe, TranslatePipe, Button, Icon, OrderDetailView],
  templateUrl: './order-tracking.html',
  styleUrl: './order-tracking.scss',
})
export class OrderTracking {
  private readonly orderService = inject(OrderService);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly orderNumber = signal(
    this.route.snapshot.queryParamMap.get('number') ?? '',
  );
  /** Opened via a `?number=` deep link (e.g. the account "view order" button): auto-runs the lookup and hides the reset. */
  protected readonly deepLinked = !!this.route.snapshot.queryParamMap.get('number');
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly result = signal<OrderTrackingDto | null>(null);

  protected readonly stages = TRACK_STAGES;

  /** Active locale for date formatting; prices stay Western (en-US via MoneyPipe default). */
  protected readonly locale = computed(() =>
    this.language.lang() === 'ar' ? 'ar' : 'en-US',
  );

  protected readonly canSubmit = computed(
    () => this.orderNumber().trim().length > 0,
  );

  protected readonly cancelled = computed(() =>
    isCancelled(this.result()?.orderStatus ?? 0),
  );
  protected readonly stageIdx = computed(() =>
    stageIndex(this.result()?.orderStatus ?? 0),
  );

  /** Milestones newest-first for the timeline (backend returns them oldest-first). */
  protected readonly events = computed<OrderTrackingEventDto[]>(() =>
    [...(this.result()?.history ?? [])].reverse(),
  );
  protected readonly latestDate = computed(
    () => this.events()[0]?.createdOn ?? this.result()?.createdOn ?? null,
  );

  protected readonly heroClass = computed(() => {
    if (this.cancelled()) {
      return 'hero is-cancelled';
    }
    return this.stageIdx() >= 3 ? 'hero is-done' : 'hero is-progress';
  });
  protected readonly heroIcon = computed<IconName>(() => {
    if (this.cancelled()) {
      return 'x';
    }
    return this.stageIdx() >= 3 ? 'check' : 'truck';
  });

  /** Localized status label (shared helper): i18n key by code, else the raw backend name. */
  protected readonly statusLabel = statusLabel;

  private autoTried = false;

  constructor() {
    // Deep-linked from the account / confirmation page: run the lookup as soon as the number is ready.
    effect(() => {
      if (this.deepLinked && !this.autoTried && !this.result() && this.canSubmit()) {
        this.autoTried = true;
        this.runTrack();
      }
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    if (!this.canSubmit() || this.loading()) {
      return;
    }
    this.runTrack();
  }

  private runTrack(): void {
    this.loading.set(true);
    this.error.set('');
    this.result.set(null);
    this.orderService.track(this.orderNumber().trim()).subscribe({
      next: (order) => {
        this.result.set(order);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(this.translate.instant('tracking.not_found'));
        this.loading.set(false);
      },
    });
  }

  /** Clear the result and return to the lookup form to track a different order. */
  protected reset(): void {
    this.result.set(null);
    this.error.set('');
    this.orderNumber.set('');
  }
}
