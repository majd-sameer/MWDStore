import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import type { OrderSummaryDto } from 'data-access';
import { Icon, Tag } from 'ui';
import { statusLabel } from './order-status';
import { TrackBar } from './track-bar';

/**
 * Order summary card for the account page: order number, date, status tag,
 * a TrackBar timeline, item count + total and a link into the order detail.
 * The status label comes from the API; the timeline derives from the code.
 */
@Component({
  selector: 'app-order-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, DatePipe, TranslatePipe, Icon, Tag, TrackBar],
  host: { class: 'order-card' },
  template: `
    <div class="oc-head">
      <div>
        <a class="oc-no" [routerLink]="['/account/orders', order().id]">
          {{ 'account.order_no' | translate: { id: order().id } }}
        </a>
        <div class="oc-date">{{ order().createdOn | date: 'mediumDate' : '' : locale() }}</div>
      </div>
      <lib-tag tone="indigo">{{
        statusLabel(order().orderStatus, order().orderStatusName) | translate
      }}</lib-tag>
    </div>

    <app-track-bar [status]="order().orderStatus" />

    <div class="oc-foot">
      <span class="oc-meta">
        {{ 'account.items' | translate: { count: order().itemCount } }} ·
        <strong class="tabular-nums">{{ order().orderTotal | money }}</strong>
      </span>
      <a class="oc-view" routerLink="/track-order" [queryParams]="{ number: order().trackingNumber }">
        {{ 'account.view' | translate }} <lib-icon name="arrowEnd" [size]="15" />
      </a>
    </div>
  `,
  styles: `
    :host {
      display: block;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 1.25rem 1.4rem;
      box-shadow: var(--shadow-sm);
    }
    .oc-head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      margin-block-end: 1.25rem;
    }
    .oc-no {
      font-weight: 700;
      color: var(--ink);
      text-decoration: none;
    }
    .oc-no:hover {
      color: var(--accent);
    }
    .oc-date {
      font-size: 0.85rem;
      color: var(--ink-3);
    }
    .oc-foot {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-block-start: 1.25rem;
      padding-block-start: 1rem;
      border-block-start: 1px solid var(--line-2);
    }
    .oc-meta {
      color: var(--ink-2);
      font-size: 0.92rem;
    }
    .oc-view {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      color: var(--ink);
      font-weight: 600;
      text-decoration: none;
    }
    .oc-view:hover {
      color: var(--accent);
    }
  `,
})
export class OrderCard {
  private readonly language = inject(LanguageService);
  readonly order = input.required<OrderSummaryDto>();

  /** Active locale for date formatting; prices stay Western (en-US). */
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));
  protected readonly statusLabel = statusLabel;
}
