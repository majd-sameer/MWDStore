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
  template: `
    <main class="wrap track">
      <h1 class="title">{{ 'tracking.title' | translate }}</h1>
      <p class="sub">{{ 'tracking.subtitle' | translate }}</p>

      @if (!result()) {
      <form class="card lookup" (submit)="onSubmit($event)" novalidate>
        <div class="lookup-fields">
          <div class="field">
            <label for="tk-num">{{ 'tracking.order_number' | translate }}</label>
            <input id="tk-num" inputmode="numeric" maxlength="6" [value]="orderNumber()"
              (input)="orderNumber.set($any($event.target).value)"
              [placeholder]="'tracking.order_number_ph' | translate" />
          </div>
        </div>
        <button libButton variant="primary" size="lg" class="lookup-btn"
          [disabled]="!canSubmit() || loading()">
          <lib-icon name="search" [size]="18" />
          {{ (loading() ? 'tracking.searching' : 'tracking.track_cta') | translate }}
        </button>
        @if (error()) {
          <p class="err">{{ error() }}</p>
        }
      </form>
      }

      @if (result(); as o) {
        <section class="result">
          @if (!deepLinked) {
            <button libButton variant="link" class="track-another" (click)="reset()">
              <lib-icon name="arrowStart" [size]="16" />
              {{ 'tracking.track_another' | translate }}
            </button>
          }

          <!-- ===== Status hero ===== -->
          <div class="hero" [class]="heroClass()">
            <div class="hero-head">
              <div class="hero-left">
                <span class="hero-ic"><lib-icon [name]="heroIcon()" [size]="26" /></span>
                <div>
                  <span class="hero-order tabular-nums">{{ 'tracking.order' | translate }} #{{ o.detail.id }}</span>
                  <b class="hero-status">{{ statusLabel(o.orderStatus, o.orderStatusName) | translate }}</b>
                </div>
              </div>
              <div class="hero-updated">
                <span>{{ 'tracking.last_update' | translate }}</span>
                <time>{{ latestDate() | date: 'medium' : '' : locale() }}</time>
              </div>
            </div>

            @if (!cancelled()) {
              <ol class="steps">
                @for (stage of stages; track stage; let i = $index) {
                  <li class="step" [class.done]="i <= stageIdx()" [class.current]="i === stageIdx()">
                    <span class="step-dot">
                      @if (i < stageIdx()) {
                        <lib-icon name="check" [size]="13" />
                      }
                    </span>
                    <span class="step-label">{{ 'track.' + stage | translate }}</span>
                  </li>
                }
              </ol>
            } @else {
              <div class="cancel-banner">
                <lib-icon name="x" [size]="16" /> {{ 'track.cancelled' | translate }}
              </div>
            }
          </div>

          <!-- ===== Full order detail (items + totals + address) ===== -->
          <div class="card">
            <app-order-detail-view [order]="o.detail" />
          </div>



          <!-- ===== Timeline (main) and details (aside) ===== -->
          <div class="grid">
            <div class="main">
              <div class="card tl-card">
                <h2 class="block-h">{{ 'tracking.history' | translate }}</h2>
                <ol class="timeline">
                  @for (e of events(); track $index; let first = $first) {
                    <li class="tl-item" [class.is-latest]="first">
                      <span class="tl-marker">
                        @if (first) { <lib-icon name="check" [size]="12" /> }
                      </span>
                      <div class="tl-body">
                        <b class="tl-status">{{ statusLabel(e.status, e.statusName) | translate }}</b>
                        <time class="tl-date">{{ e.createdOn | date: 'medium' : '' : locale() }}</time>
                      </div>
                    </li>
                  }
                </ol>
              </div>
            </div>

            <aside class="card details">
              <h2 class="block-h">{{ 'tracking.details' | translate }}</h2>
              <div class="row mb-2">
                <div class="col-12 col-md-6">
                  <span>{{ 'tracking.placed_on' | translate }} : </span>
                  <b>{{ o.createdOn | date: 'mediumDate' : '' : locale() }}</b>
                </div>
                @if (o.shippingMethod) {
                <div class="col-12 col-md-6">
                  <span>{{ 'tracking.shipping' | translate }} : </span>
                  <b>{{ o.shippingMethod }}</b>
                </div>
                }
                @if (o.paymentMethod) {
                  <div class="col-12 col-md-6">
                    <span>{{ 'tracking.payment' | translate }} : </span>
                    <b>{{ o.paymentMethod }}</b>
                  </div>
                }
                <div class="col-12 col-md-6">
                  <span>{{ 'tracking.total' | translate }} : </span>
                  <strong class="tabular-nums">{{ o.orderTotal | money }}</strong>
                </div>
              </div>
            </aside>
          </div>
        </section>
      }
    </main>
  `,
  styles: `
    :host {
      display: block;
    }
    .track {
      padding-block: 32px 60px;
      max-inline-size: 1040px;
      margin-inline: auto;
    }
    .title {
      font-weight: 700;
      font-size: clamp(1.8rem, 4vw, 2.4rem);
    }
    .sub {
      color: var(--ink-2);
      margin-block: 0.5rem 1.75rem;
    }
    .card {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 22px 24px;
    }

    /* ----- Lookup form ----- */
    .lookup {
      max-inline-size: 620px;
      margin-inline: auto;
    }
    .lookup-fields {
      display: grid;
      grid-template-columns: 1fr;
      gap: 14px;
    }
    .field label {
      display: block;
      font-size: 0.88rem;
      font-weight: 600;
      margin-block-end: 7px;
    }
    .field input {
      inline-size: 100%;
      border: 1.5px solid var(--line);
      border-radius: var(--r-sm);
      padding: 12px 14px;
      background: var(--surface);
      color: var(--ink);
      font: inherit;
    }
    .field input:focus {
      outline: none;
      border-color: var(--navy);
    }
    .field input:disabled {
      background: var(--surface-2);
      color: var(--ink-2);
      cursor: not-allowed;
    }
    .hint {
      display: block;
      margin-block-start: 6px;
      font-size: 0.8rem;
      color: var(--ink-3);
    }
    .lookup-btn {
      margin-block-start: 16px;
    }
    .err {
      margin-block: 14px 0;
      color: #b0492c;
      font-size: 0.9rem;
    }

    /* ----- Result ----- */
    .result {
      margin-block-start: 24px;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    .track-another {
      align-self: flex-start;
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding-inline: 0;
      text-decoration: none;
      font-weight: 600;
    }

    /* ----- Status hero ----- */
    .hero {
      border-radius: var(--r-lg);
      padding: 24px 26px;
      color: #fff;
      background: linear-gradient(135deg, var(--navy), var(--navy-deep));
      box-shadow: var(--shadow, 0 10px 30px rgba(0, 0, 0, 0.12));
    }
    .hero.is-done {
      background: linear-gradient(135deg, var(--green), var(--green-strong));
    }
    .hero.is-cancelled {
      background: linear-gradient(135deg, #b0492c, #7e3220);
    }
    .hero-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }
    .hero-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .hero-ic {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 50px;
      block-size: 50px;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.16);
      color: #fff;
    }
    .hero-order {
      display: block;
      font-size: 0.85rem;
      opacity: 0.85;
    }
    .hero-status {
      font-size: 1.5rem;
      font-weight: 700;
      line-height: 1.2;
    }
    .hero-updated {
      text-align: end;
      font-size: 0.85rem;
      opacity: 0.9;
    }
    .hero-updated time {
      display: block;
      font-weight: 600;
    }

    /* ----- Progress steps ----- */
    .steps {
      display: flex;
      list-style: none;
      margin: 26px 0 4px;
      padding: 0;
    }
    .step {
      flex: 1 1 0;
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.8rem;
      text-align: center;
      color: rgba(255, 255, 255, 0.7);
    }
    .step::before {
      content: '';
      position: absolute;
      inset-block-start: 12px;
      inset-inline-end: 50%;
      inline-size: 100%;
      block-size: 3px;
      background: rgba(255, 255, 255, 0.25);
    }
    .step:first-child::before {
      display: none;
    }
    .step.done::before {
      background: var(--gold, #d8b863);
    }
    .step-dot {
      position: relative;
      z-index: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 26px;
      block-size: 26px;
      border-radius: 50%;
      /* Opaque (same tone as the old translucent white, but solid) so the
         connector line behind it doesn't show through the dot. */
      background: color-mix(in srgb, #fff 25%, var(--navy-deep));
      color: var(--navy-deep);
    }
    .step.done .step-dot {
      background: var(--gold, #d8b863);
    }
    .step.current .step-dot {
      box-shadow: 0 0 0 4px rgba(255, 255, 255, 0.25);
    }
    .step.done .step-label,
    .step.current .step-label {
      color: #fff;
      font-weight: 600;
    }
    .cancel-banner {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      margin-block-start: 18px;
      padding: 8px 14px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.16);
      font-weight: 600;
      font-size: 0.9rem;
    }

    /* ----- Items + timeline (main) / details (aside) ----- */
    .grid {
      display: grid;
      grid-template-columns: 1fr 50%;
      gap: 20px;
      align-items: start;
    }
    @media (max-width: 760px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
    .main {
      display: flex;
      flex-direction: column;
      gap: 20px;
      min-inline-size: 0;
    }
    .details {
      position: sticky;
      inset-block-start: 84px;
    }
    @media (max-width: 760px) {
      .details {
        position: static;
      }
    }
    .block-h {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 1.05rem;
      font-weight: 700;
      margin-block-end: 1.1rem;
    }
    /* ----- Vertical timeline ----- */
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .tl-item {
      position: relative;
      padding-inline-start: 30px;
      padding-block-end: 22px;
    }
    .tl-item:last-child {
      padding-block-end: 0;
    }
    /* connector line */
    .tl-item::before {
      content: '';
      position: absolute;
      inset-block: 4px 0;
      inset-inline-start: 8px;
      inline-size: 2px;
      background: var(--line-strong, var(--line));
    }
    .tl-item:last-child::before {
      display: none;
    }
    .tl-marker {
      position: absolute;
      inset-inline-start: 0;
      inset-block-start: 2px;
      z-index: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 18px;
      block-size: 18px;
      border-radius: 50%;
      background: var(--accent);
      color: #fff;
    }
    .tl-item.is-latest .tl-marker {
      background: var(--green-strong);
      box-shadow: 0 0 0 4px var(--green-soft);
    }
    .tl-body {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .tl-status {
      font-size: 0.98rem;
      color: var(--ink);
    }
    .tl-item:not(.is-latest) .tl-status {
      color: var(--ink-2);
      font-weight: 600;
    }
    .tl-date {
      font-size: 0.82rem;
      color: var(--ink-3);
    }

    /* ----- Details ----- */
    .details .row {
      color: var(--ink-2);
     //border-block-end: 1px solid var(--line-2, var());
    }
    .details .row:last-child {
      border-block-end: 0;
    }
    .details .row b {
      color: var(--ink);
    }
    .details .row.total {
    /*  margin-block-start: 6px;
      padding-block-start: 14px;
      border-block-start: 1px solid var(--line);
      border-block-end: 0;*/
      border-top: dotted 1px  var(--line);
      font-size: 1.15rem;
      font-weight: 700;
      color: var(--ink);
    }
  `,
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
