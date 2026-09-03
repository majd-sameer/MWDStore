import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { CartStore } from '../core/cart.store';
import { announceCartError, cartAdjustmentMessage } from '../core/cart-messages';

/**
 * Inline "in your bag" control shown wherever a product can be added: a label so the shopper knows
 * the item is already in the bag, plus a −/qty/+ stepper that updates the bag right there, without
 * opening the drawer. At quantity 1 the minus becomes a bin, so one tap takes the item out again.
 *
 * Renders only while the product is in the bag; otherwise it projects its content (the ordinary
 * add button), so call sites keep their own add handling.
 */
@Component({
  selector: 'app-bag-qty',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, Icon],
  template: `
    @if (line(); as line) {
      <div class="bq" [class.bq-md]="size() === 'md'" [class.bq-busy]="busy()">
        <span class="bq-label">
          <lib-icon name="check" [size]="14" />
          {{ 'cart.in_bag' | translate }}
          @if (size() === 'md') {
            <a class="bq-view" routerLink="/cart">{{ 'cart.view_bag' | translate }}</a>
          }
        </span>
        <div class="bq-stepper" role="group" [attr.aria-label]="'product.qty' | translate">
          <button
            type="button"
            class="bq-btn"
            [class.bq-btn-remove]="line.quantity <= 1"
            [disabled]="busy()"
            [attr.aria-label]="(line.quantity <= 1 ? 'cart.remove' : 'cart.decrease') | translate"
            (click)="change(line.quantity - 1)"
          >
            <lib-icon [name]="line.quantity <= 1 ? 'trash' : 'minus'" [size]="15" />
          </button>
          <span class="bq-value tabular-nums" aria-live="polite">{{ line.quantity }}</span>
          <button
            type="button"
            class="bq-btn"
            [disabled]="busy() || line.quantity >= max()"
            [attr.aria-label]="'cart.increase' | translate"
            (click)="change(line.quantity + 1)"
          >
            <lib-icon name="plus" [size]="15" />
          </button>
        </div>
      </div>
    } @else {
      <ng-content />
    }
  `,
  styles: `
    :host {
      display: contents;
    }
    .bq {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 0.25rem;
    }
    .bq-label {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      font-size: 0.72rem;
      font-weight: 700;
      color: var(--green);
      white-space: nowrap;
    }
    .bq-stepper {
      display: inline-flex;
      align-items: center;
      border: 1.5px solid var(--green);
      border-radius: 999px;
      background: var(--surface);
      overflow: hidden;
    }
    .bq-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 34px;
      block-size: 34px;
      border: 0;
      background: transparent;
      color: var(--green);
      cursor: pointer;
    }
    .bq-btn:hover:not(:disabled) {
      background: color-mix(in srgb, var(--green) 14%, transparent);
    }
    .bq-btn:disabled {
      color: var(--ink-3);
      cursor: not-allowed;
    }
    .bq-btn-remove {
      color: var(--ink-2);
    }
    .bq-value {
      min-inline-size: 1.6rem;
      text-align: center;
      font-weight: 700;
      color: var(--ink);
    }
    .bq-busy .bq-value {
      opacity: 0.5;
    }
    /* md: product page — bigger targets, label and stepper side by side. */
    .bq-md {
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 0.75rem 1rem;
      padding: 0.85rem 1rem;
      border-radius: var(--r-sm);
      background: color-mix(in srgb, var(--green) 10%, transparent);
    }
    .bq-md .bq-label {
      font-size: 0.95rem;
      gap: 0.5rem;
    }
    .bq-view {
      margin-inline-start: 0.5rem;
      font-weight: 600;
      color: var(--ink-2);
      text-decoration: underline;
      text-underline-offset: 3px;
    }
    .bq-view:hover {
      color: var(--accent);
    }
    .bq-md .bq-btn {
      inline-size: 42px;
      block-size: 42px;
    }
    .bq-md .bq-value {
      min-inline-size: 2.2rem;
      font-size: 1.05rem;
    }
  `,
})
export class BagQty {
  /** The product (or variation) id whose bag line this control edits. */
  readonly productId = input.required<number>();
  readonly size = input<'sm' | 'md'>('sm');

  private readonly store = inject(CartStore);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly busy = signal(false);

  protected readonly line = computed(
    () => this.store.items().find((item) => item.productId === this.productId()) ?? null,
  );

  protected readonly max = computed(() => {
    const line = this.line();
    return line?.productStockTrackingIsEnabled ? Math.max(1, line.availableQuantity) : 99;
  });

  protected change(quantity: number): void {
    const line = this.line();
    if (!line || this.busy()) {
      return;
    }
    this.busy.set(true);
    const fail = (error: unknown): void => {
      this.busy.set(false);
      announceCartError(this.toast, this.translate, error);
    };
    if (quantity < 1) {
      this.store.remove(line.id).subscribe({ next: () => this.busy.set(false), error: fail });
      return;
    }
    this.store.update(line.id, { quantity }).subscribe({
      next: (cart) => {
        this.busy.set(false);
        const capped = cartAdjustmentMessage(cart);
        if (capped) {
          this.toast.success(this.translate.instant(capped.key, capped.params));
        }
      },
      error: fail,
    });
  }
}
